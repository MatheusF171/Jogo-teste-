using UnityEngine;
using UnityEngine.UI;

namespace RotaDoCangaco
{
    public enum HeroPose { Idle, Walk, Crouch, Air, Cast, Hurt }

    /// <summary>
    /// Anima o herói com os quadros heroi_f1..f8. Funciona em SpriteRenderer (mapa) e em Image (interface).
    /// Quadros: 3-4 parado · 6-7-8 andando · 2 agachado · 1 no ar / lançando feitiço · 5 gordo.
    /// </summary>
    public class HeroAnimator : MonoBehaviour
    {
        static readonly int[] IdleFrames = { 3, 4 };
        static readonly int[] WalkFrames = { 6, 7, 8, 7 };
        static readonly float[] FatWidth = { 1f, 1.12f, 1f, 1.15f };
        static readonly Sprite[] frames = new Sprite[9];

        SpriteRenderer spriteRenderer;
        Image image;
        Vector3 baseScale = Vector3.one;
        HeroPose pose = HeroPose.Idle;
        float poseUntil = -1f;
        float clock;
        int fatStage;

        public HeroPose Pose => pose;

        public static Sprite Frame(int index)
        {
            if (frames[index] == null) frames[index] = SpriteLibrary.Get("heroi_f" + index);
            return frames[index];
        }

        /// <summary>Adiciona o animador a um objeto que já tenha SpriteRenderer ou Image.</summary>
        public static HeroAnimator Attach(GameObject go)
        {
            var anim = go.AddComponent<HeroAnimator>();
            anim.spriteRenderer = go.GetComponent<SpriteRenderer>();
            anim.image = go.GetComponent<Image>();
            anim.baseScale = go.transform.localScale;
            anim.clock = Random.value * 2f;
            anim.Apply();
            return anim;
        }

        /// <summary>Escala "normal" do objeto (a largura extra de gordura é aplicada por cima).</summary>
        public void SetBaseScale(Vector3 scale)
        {
            baseScale = scale;
            Apply();
        }

        public void SetPose(HeroPose newPose)
        {
            pose = newPose;
            poseUntil = -1f;
            Apply();
        }

        /// <summary>Pose temporária (ex.: lançar feitiço, levar dano) que volta a ficar parado depois.</summary>
        public void PlayOnce(HeroPose newPose, float duration)
        {
            pose = newPose;
            poseUntil = Time.time + duration;
            Apply();
        }

        /// <summary>Alterna entre parado e andando, sem interromper poses especiais.</summary>
        public void SetWalking(bool walking)
        {
            if (poseUntil > 0f || (pose != HeroPose.Idle && pose != HeroPose.Walk)) return;
            pose = walking ? HeroPose.Walk : HeroPose.Idle;
        }

        /// <summary>0 normal · 1 bucho cheio (mais largo) · 2 gordinho (quadro 5) · 3 gordo (quadro 5 mais largo).</summary>
        public void SetFatStage(int stage)
        {
            stage = Mathf.Clamp(stage, 0, FatWidth.Length - 1);
            if (stage == fatStage) return;
            fatStage = stage;
            Apply();
        }

        void Update()
        {
            if (poseUntil > 0f && Time.time >= poseUntil)
            {
                pose = HeroPose.Idle;
                poseUntil = -1f;
            }
            clock += Time.deltaTime;
            Apply();
        }

        int CurrentFrame()
        {
            if (fatStage >= 2) return 5;
            switch (pose)
            {
                case HeroPose.Walk: return WalkFrames[(int)(clock * 10f) % WalkFrames.Length];
                case HeroPose.Crouch: return 2;
                case HeroPose.Air:
                case HeroPose.Cast: return 1;
                case HeroPose.Hurt: return 6;
                default: return IdleFrames[(int)(clock / 0.45f) % IdleFrames.Length];
            }
        }

        void Apply()
        {
            var sprite = Frame(CurrentFrame());
            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
            if (image != null) image.sprite = sprite;

            float width = FatWidth[fatStage];
            // Gordo "respira" mais devagar e mais largo.
            float breathe = fatStage >= 2 ? 1f + Mathf.Sin(clock * 3f) * 0.03f : 1f;
            transform.localScale = new Vector3(baseScale.x * width * breathe, baseScale.y / Mathf.Sqrt(breathe), baseScale.z);
        }
    }
}
