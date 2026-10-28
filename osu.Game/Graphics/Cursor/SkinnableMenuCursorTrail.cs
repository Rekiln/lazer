// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
#nullable disable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Visualisation;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Menu equivalent of osu!'s gameplay LegacyCursorTrail. This intentionally uses
    /// the same GPU trail shader and vertex path as gameplay rather than approximating
    /// the trail with individual Sprite drawables.
    /// </summary>
    [DrawVisualiserHidden]
    public partial class SkinnableMenuCursorTrail : Drawable, IRequireHighFrequencyMousePosition
    {
        private const int max_sprites = 2048;
        private const double disjoint_trail_time_separation = 1000 / 60.0;

        private readonly TrailPart[] parts = new TrailPart[max_sprites];
        private int currentIndex;
        private IShader shader;
        private Texture texture;
        private Texture fallbackTexture;

        public Texture Texture
        {
            get => texture;
            private set
            {
                if (texture == value)
                    return;

                texture = value;
                Invalidate(Invalidation.DrawNode);
            }
        }
        private float time;
        private double timeOffset;
        private double lastTrailTime;
        private Vector2? currentPosition;
        private Vector2? lastPosition;
        private InputResampler resampler = new InputResampler();
        private IBindable<float> cursorSize;
        private IBindable<bool> useSkinCursorInMenus;
        private ISkinSource skinSource;
        private Texture activeSkinTrailTexture;
        private bool trailEnabled;
        private bool disjointTrail;
        private bool allowPartRotation;
        private Anchor trailOrigin = Anchor.Centre;
        private Vector2 cursorScale = Vector2.One;
        private Vector2 newPartScale = Vector2.One;
        private bool cursorCentre = true;

        /// <summary>
        /// The size multiplier used when drawing trail segments. Kept in sync with the menu cursor.
        /// </summary>
        public Vector2 CursorScale
        {
            get => cursorScale;
            set
            {
                if (cursorScale == value)
                    return;

                cursorScale = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The current cursor-head expansion, sampled for newly emitted trail pieces.
        /// Kept separate from <see cref="CursorScale"/>, matching gameplay's CursorTrail.
        /// </summary>
        public Vector2 NewPartScale
        {
            get => newPartScale;
            set => newPartScale = value;
        }

        /// <summary>
        /// Cursor rotation to apply to trail segments when the selected skin allows rotation.
        /// </summary>
        public float PartRotation { get; set; }

        public SkinnableMenuCursorTrail()
        {
            Clock = new FramedClock();
            RelativeSizeAxes = Axes.Both;

            for (int i = 0; i < parts.Length; i++)
                parts[i].InvalidationID = -1;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders, OsuConfigManager config, ISkinSource skinSource)
        {
            fallbackTexture = renderer.WhitePixel;
            texture ??= fallbackTexture;
            shader = shaders.Load(@"CursorTrail", FragmentShaderDescriptor.TEXTURE);
            cursorSize = config.GetBindable<float>(OsuSetting.GameplayCursorSize).GetBoundCopy();
            this.skinSource = skinSource;

            useSkinCursorInMenus = config.GetBindable<bool>(OsuSetting.UseSkinCursorInMenus);
            useSkinCursorInMenus.BindValueChanged(change =>
            {
                if (!change.NewValue)
                {
                    trailEnabled = false;
                    clearTrailParts();
                }

                Schedule(refreshTrail);
            }, true);

            skinSource.SourceChanged += onSkinSourceChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            resetTime();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= onSkinSourceChanged;

            base.Dispose(isDisposing);
        }

        private void onSkinSourceChanged()
        {
            Schedule(() =>
            {
                clearTrailParts();
                refreshTrail();
            });
        }

        private void refreshTrail()
        {
            ISkin cursorProvider = useSkinCursorInMenus.Value
                ? skinSource.FindProvider(s => s.GetTexture(@"cursor") != null)
                : null;

            ISkin trailProvider = cursorProvider != null
                ? skinSource.FindProvider(s => s.GetTexture(@"cursortrail") != null)
                : null;

            Texture nextTexture = trailProvider?.GetTexture(@"cursortrail");
            bool nextEnabled = cursorProvider != null && nextTexture != null;
            bool nextDisjoint = cursorProvider?.GetTexture(@"cursormiddle") == null;
            bool nextCentre = trailProvider?.GetConfig<MenuCursorSkinConfiguration, bool>(MenuCursorSkinConfiguration.CursorCentre)?.Value ?? true;
            bool nextRotate = trailProvider?.GetConfig<MenuCursorSkinConfiguration, bool>(MenuCursorSkinConfiguration.CursorTrailRotate)?.Value ?? true;

            bool changed = activeSkinTrailTexture != nextTexture
                           || trailEnabled != nextEnabled
                           || disjointTrail != nextDisjoint
                           || cursorCentre != nextCentre
                           || allowPartRotation != nextRotate;

            if (changed)
                clearTrailParts();

            if (activeSkinTrailTexture != nextTexture)
            {
                activeSkinTrailTexture = nextTexture;

                // The gameplay trail is drawn under the osu! playfield's 1.6x transform,
                // so LegacyCursorTrail compensates by adjusting its texture ScaleAdjust.
                // The menu trail is already in window coordinates and has no such transform;
                // applying that adjustment here incorrectly shrinks its trail pieces.
                Texture = activeSkinTrailTexture ?? fallbackTexture;
            }

            trailEnabled = nextEnabled;
            disjointTrail = nextDisjoint;
            cursorCentre = nextCentre;
            allowPartRotation = nextRotate;
            trailOrigin = disjointTrail && !cursorCentre ? Anchor.TopLeft : Anchor.Centre;
            Blending = disjointTrail ? BlendingParameters.Inherit : BlendingParameters.Additive;
            lastTrailTime = Time.Current;
        }

        /// <summary>
        /// Immediately discard any existing trail segments. Called when the option is
        /// disabled or the current skin changes so old segments cannot remain on screen.
        /// </summary>
        public void ClearTrail() => clearTrailParts();

        private void clearTrailParts()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].InvalidationID = -1;
                parts[i].Time = 0;
            }

            currentIndex = 0;
            currentPosition = null;
            lastPosition = null;
            resampler = new InputResampler();
            lastTrailTime = Time.Current;
            Invalidate(Invalidation.DrawNode);
        }

        // Keep these skin.ini lookups in osu.Game without a compile-time dependency on
        // osu.Game.Rulesets.Osu. LegacySkin resolves generic enum lookups by key name.
        private enum MenuCursorSkinConfiguration
        {
            CursorCentre,
            CursorTrailRotate,
        }

        private double FadeDuration => disjointTrail ? 150 : 500;
        private const float fade_exponent = 1;
        private float intervalMultiplier => 1 / Math.Max(cursorSize.Value, 1);
        private bool avoidDrawingNearCursor => !disjointTrail;

        public override bool IsPresent => true;

        protected override void Update()
        {
            base.Update();

            Invalidate(Invalidation.DrawNode);

            const int fade_clock_reset_threshold = 1000000;
            time = (float)((Time.Current - timeOffset) / FadeDuration);
            if (time > fade_clock_reset_threshold)
                resetTime();

            if (!trailEnabled || !disjointTrail || !currentPosition.HasValue)
                return;

            if (Time.Current - lastTrailTime >= disjoint_trail_time_separation)
            {
                lastTrailTime = Time.Current;
                addTrail(currentPosition.Value);
            }
        }

        private void resetTime()
        {
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].Time -= time;
                if (parts[i].InvalidationID != -1)
                    ++parts[i].InvalidationID;
            }

            time = 0;
            timeOffset = Time.Current;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        /// <summary>
        /// Receives the menu cursor's high-frequency mouse movement. Kept explicit so
        /// the trail is updated even while the pointer is over interactive UI elements.
        /// </summary>
        public void HandleMouseMove(Vector2 screenSpacePosition)
        {
            if (!trailEnabled)
                return;

            if (disjointTrail)
            {
                currentPosition = screenSpacePosition;
                return;
            }

            addTrail(screenSpacePosition);
        }

        // The parent MenuCursorContainer forwards the input. This drawable still
        // advertises high-frequency position requirements to the input system, but
        // must not add a duplicate trail when the same event reaches this child.
        protected override bool OnMouseMove(MouseMoveEvent e) => false;

        private void addTrail(Vector2 screenSpacePosition)
        {
            if (!trailEnabled || texture == null)
                return;

            Vector2 position = ToLocalSpace(screenSpacePosition);

            // LegacyCursorTrail adds one full texture segment for each time-based sample
            // when cursormiddle is absent; it does not interpolate between samples.
            if (disjointTrail)
            {
                lastPosition = position;
                addPart(position);
                return;
            }

            if (!lastPosition.HasValue)
            {
                lastPosition = position;
                resampler.AddPosition(position);
                return;
            }

            foreach (Vector2 nextPosition in resampler.AddPosition(position))
            {
                Trace.Assert(lastPosition.HasValue);
                Vector2 previous = lastPosition.Value;
                Vector2 difference = nextPosition - previous;
                float distance = difference.Length;

                if (distance <= 0)
                    continue;

                Vector2 direction = difference / distance;
                float interval = texture.DisplayWidth * CursorScale.X / 2.5f * intervalMultiplier;
                float stopAt = distance - (avoidDrawingNearCursor ? interval : 0);

                for (float travelled = interval; travelled < stopAt; travelled += interval)
                {
                    lastPosition = previous + direction * travelled;
                    addPart(lastPosition.Value);
                }
            }
        }

        private void addPart(Vector2 position)
        {
            parts[currentIndex].Position = position;
            parts[currentIndex].Time = time + 1;
            parts[currentIndex].Scale = NewPartScale;
            ++parts[currentIndex].InvalidationID;
            currentIndex = (currentIndex + 1) % max_sprites;
        }

        protected override DrawNode CreateDrawNode() => new TrailDrawNode(this);

        private struct TrailPart
        {
            public Vector2 Position;
            public float Time;
            public Vector2 Scale;
            public long InvalidationID;
        }

        private class TrailDrawNode : DrawNode
        {
            protected new SkinnableMenuCursorTrail Source => (SkinnableMenuCursorTrail)base.Source;

            private IShader shader;
            private Texture texture;
            private float time;
            private float fadeExponent;
            private float angle;
            private Vector2 cursorScale;
            private Anchor trailOrigin;
            private readonly TrailPart[] parts = new TrailPart[max_sprites];
            private Vector2 originPosition;
            private IVertexBatch<TexturedTrailVertex> vertexBatch;
            private IUniformBuffer<CursorTrailParameters> cursorTrailParameters;

            public TrailDrawNode(SkinnableMenuCursorTrail source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                time = Source.time;
                fadeExponent = fade_exponent;
                angle = Source.allowPartRotation ? float.DegreesToRadians(Source.PartRotation) : 0;
                cursorScale = Source.cursorScale;
                trailOrigin = Source.trailOrigin;
                Source.parts.CopyTo(parts, 0);

                originPosition = Vector2.Zero;
                if (trailOrigin.HasFlag(Anchor.x1))
                    originPosition.X = 0.5f;
                else if (trailOrigin.HasFlag(Anchor.x2))
                    originPosition.X = 1f;

                if (trailOrigin.HasFlag(Anchor.y1))
                    originPosition.Y = 0.5f;
                else if (trailOrigin.HasFlag(Anchor.y2))
                    originPosition.Y = 1f;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                vertexBatch ??= renderer.CreateQuadBatch<TexturedTrailVertex>(max_sprites, 1);
                cursorTrailParameters ??= renderer.CreateUniformBuffer<CursorTrailParameters>();
                cursorTrailParameters.Data = cursorTrailParameters.Data with
                {
                    FadeClock = time,
                    FadeExponent = fadeExponent
                };

                shader.Bind();
                shader.BindUniformBlock("m_CursorTrailParameters", cursorTrailParameters);
                texture.Bind();
                RectangleF textureRect = texture.GetTextureRect();
                renderer.PushLocalMatrix(DrawInfo.Matrix);

                float sin = MathF.Sin(angle);
                float cos = MathF.Cos(angle);

                foreach (TrailPart part in parts)
                {
                    if (part.InvalidationID == -1 || time - part.Time >= 1)
                        continue;

                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(new Vector2(
                            part.Position.X - texture.DisplayWidth * originPosition.X * part.Scale.X * cursorScale.X,
                            part.Position.Y + texture.DisplayHeight * (1 - originPosition.Y) * part.Scale.Y * cursorScale.Y), part.Position, sin, cos),
                        TexturePosition = textureRect.BottomLeft,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.BottomLeft.Linear,
                        Time = part.Time
                    });
                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(new Vector2(
                            part.Position.X + texture.DisplayWidth * (1 - originPosition.X) * part.Scale.X * cursorScale.X,
                            part.Position.Y + texture.DisplayHeight * (1 - originPosition.Y) * part.Scale.Y * cursorScale.Y), part.Position, sin, cos),
                        TexturePosition = textureRect.BottomRight,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.BottomRight.Linear,
                        Time = part.Time
                    });
                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(new Vector2(
                            part.Position.X + texture.DisplayWidth * (1 - originPosition.X) * part.Scale.X * cursorScale.X,
                            part.Position.Y - texture.DisplayHeight * originPosition.Y * part.Scale.Y * cursorScale.Y), part.Position, sin, cos),
                        TexturePosition = textureRect.TopRight,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.TopRight.Linear,
                        Time = part.Time
                    });
                    vertexBatch.Add(new TexturedTrailVertex
                    {
                        Position = rotateAround(new Vector2(
                            part.Position.X - texture.DisplayWidth * originPosition.X * part.Scale.X * cursorScale.X,
                            part.Position.Y - texture.DisplayHeight * originPosition.Y * part.Scale.Y * cursorScale.Y), part.Position, sin, cos),
                        TexturePosition = textureRect.TopLeft,
                        TextureRect = new Vector4(0, 0, 1, 1),
                        Colour = DrawColourInfo.Colour.TopLeft.Linear,
                        Time = part.Time
                    });
                }

                renderer.PopLocalMatrix();
                vertexBatch.Draw();
                shader.Unbind();
            }

            private static Vector2 rotateAround(Vector2 input, Vector2 origin, float sin, float cos)
            {
                float xTranslated = input.X - origin.X;
                float yTranslated = input.Y - origin.Y;
                return new Vector2(xTranslated * cos - yTranslated * sin, xTranslated * sin + yTranslated * cos) + origin;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                vertexBatch?.Dispose();
                cursorTrailParameters?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct CursorTrailParameters
            {
                public UniformFloat FadeClock;
                public UniformFloat FadeExponent;
                private readonly UniformPadding8 pad1;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TexturedTrailVertex : IEquatable<TexturedTrailVertex>, IVertex
        {
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 Position;
            [VertexMember(4, VertexAttribPointerType.Float)]
            public Color4 Colour;
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 TexturePosition;
            [VertexMember(4, VertexAttribPointerType.Float)]
            public Vector4 TextureRect;
            [VertexMember(1, VertexAttribPointerType.Float)]
            public float Time;

            public bool Equals(TexturedTrailVertex other)
            {
                return Position.Equals(other.Position)
                       && TexturePosition.Equals(other.TexturePosition)
                       && Colour.Equals(other.Colour)
                       && Time.Equals(other.Time);
            }
        }
    }
}
