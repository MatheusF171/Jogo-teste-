using UnityEngine;

namespace RotaDoCangaco
{
    /// <summary>
    /// Trilha sonora: faixas do menu principal, da exploração e da batalha, em loop, com troca suave (crossfade).
    /// - Menu: recomeça do início sempre que o menu principal abre.
    /// - Exploração: pausa durante as batalhas e continua de onde parou; recomeça do início numa partida nova.
    /// - Batalha: recomeça do início a cada combate.
    /// O mudo fica salvo entre as sessões (PlayerPrefs). Criado pelo GameManager.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const string MuteKey = "RotaDoCangaco.MusicaMutada";
        const string MenuPath = "Audio/musica_menu";
        const string ExplorationPath = "Audio/musica_exploracao";
        const string BattlePath = "Audio/musica_batalha";
        const float FadeDuration = 0.8f;

        // Volumes equilibrados pelo RMS medido de cada arquivo (menu -23,7 dBFS, exploração -17,7, batalha -21,6):
        // todas ficam perto de -25,5 dBFS no jogo, com a batalha um pouco mais forte.
        [Range(0f, 1f)] public float MenuVolume = 0.8f;
        [Range(0f, 1f)] public float ExplorationVolume = 0.4f;
        [Range(0f, 1f)] public float BattleVolume = 0.65f;

        enum Track { None, Menu, Exploration, Battle }

        /// <summary>Uma faixa em loop com o nível do fade (0 a 1).</summary>
        class Channel
        {
            public Track Track;
            public AudioSource Source;
            public float Level;        // fade atual
            public float Target;       // fade desejado
            public bool Paused;        // pausada no meio da faixa
            public bool KeepPosition;  // ao sumir, pausa (continua depois) em vez de parar
        }

        Channel menu, exploration, battle;
        Channel[] channels;
        Track current = Track.None;
        bool ready;
        bool muted;

        /// <summary>Música desligada pelo jogador (fica salvo entre as sessões).</summary>
        public bool Muted
        {
            get
            {
                EnsureReady();
                return muted;
            }
            set
            {
                EnsureReady();
                muted = value;
                PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
                PlayerPrefs.Save();
                ApplyMute();
            }
        }

        /// <summary>A faixa do menu principal é a atual (usado pelo teste automático).</summary>
        public bool PlayingMenu => current == Track.Menu;

        /// <summary>A faixa de batalha é a atual (usado pelo teste automático).</summary>
        public bool PlayingBattle => current == Track.Battle;

        /// <summary>A faixa de exploração é a atual (usado pelo teste automático).</summary>
        public bool PlayingExploration => current == Track.Exploration;

        /// <summary>A faixa atual está de fato tocando com volume acima de zero (usado pelo teste automático).</summary>
        public bool CurrentTrackAudible
        {
            get
            {
                EnsureReady();
                if (muted) return false;
                foreach (var ch in channels)
                    if (ch.Track == current) return ch.Source.clip != null && ch.Source.isPlaying && ch.Source.volume > 0.01f;
                return false;
            }
        }

        /// <summary>As três faixas foram encontradas em Resources/Audio.</summary>
        public bool ClipsLoaded
        {
            get
            {
                EnsureReady();
                return menu.Source.clip != null && exploration.Source.clip != null && battle.Source.clip != null;
            }
        }

        void Awake() => EnsureReady();

        /// <summary>Faixa do menu principal (e da tela "Como jogar"). Recomeça do início ao abrir o menu.</summary>
        public void PlayMenu() => Play(Track.Menu, true);

        /// <summary>
        /// Faixa da exploração (mapa e telas finais). Por padrão continua de onde parou;
        /// com <paramref name="restart"/> recomeça do início (partida nova).
        /// </summary>
        public void PlayExploration(bool restart = false) => Play(Track.Exploration, restart);

        /// <summary>Faixa de combate. Recomeça do início a cada batalha.</summary>
        public void PlayBattle() => Play(Track.Battle, true);

        // ------------------------------------------------------------------ Interno

        void Play(Track track, bool restart)
        {
            EnsureReady();
            if (current == track) return;
            current = track;
            foreach (var ch in channels)
            {
                if (ch.Track == track) FadeIn(ch, restart);
                else ch.Target = 0f;
            }
        }

        void EnsureReady()
        {
            if (ready) return;
            ready = true;
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            EnsureListener();
            menu = CreateChannel(Track.Menu, "MusicaMenu", MenuPath, false);
            exploration = CreateChannel(Track.Exploration, "MusicaExploracao", ExplorationPath, true);
            battle = CreateChannel(Track.Battle, "MusicaBatalha", BattlePath, false);
            channels = new[] { menu, exploration, battle };
            ApplyMute();
        }

        /// <summary>Sem AudioListener na cena nada toca; cria um na câmera se faltar.</summary>
        void EnsureListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            var cam = Camera.main;
            (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
        }

        Channel CreateChannel(Track track, string name, string path, bool keepPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0f;

            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning($"[Música] Faixa não encontrada em Resources/{path}. O jogo segue sem ela.");
            source.clip = clip;
            return new Channel { Track = track, Source = source, KeepPosition = keepPosition };
        }

        void FadeIn(Channel ch, bool restart)
        {
            ch.Target = 1f;
            var src = ch.Source;
            if (src == null || src.clip == null) return;

            if (restart)
            {
                src.Stop();
                ch.Paused = false;
                ch.Level = 0f;
                src.volume = 0f;
                src.Play();
            }
            else if (ch.Paused)
            {
                ch.Paused = false;
                src.UnPause();
            }
            else if (!src.isPlaying)
            {
                src.Play();
            }
        }

        void ApplyMute()
        {
            // Mudo não interrompe a faixa: ela segue tocando em silêncio e volta no ponto certo.
            if (channels == null) return;
            foreach (var ch in channels)
                if (ch != null && ch.Source != null) ch.Source.mute = muted;
        }

        float VolumeOf(Channel ch)
        {
            switch (ch.Track)
            {
                case Track.Menu: return MenuVolume;
                case Track.Battle: return BattleVolume;
                default: return ExplorationVolume;
            }
        }

        void Update()
        {
            if (!ready) return;
            // Tempo sem escala: o fade funciona mesmo com o jogo pausado; limita saltos depois de travadas.
            float step = Mathf.Min(Time.unscaledDeltaTime, 0.1f) / FadeDuration;
            foreach (var ch in channels) Tick(ch, VolumeOf(ch), step);
        }

        void Tick(Channel ch, float volume, float step)
        {
            if (ch == null || ch.Source == null) return;
            var src = ch.Source;
            ch.Level = Mathf.MoveTowards(ch.Level, ch.Target, step);
            src.volume = Mathf.SmoothStep(0f, 1f, ch.Level) * volume;

            if (ch.Level <= 0f && ch.Target <= 0f && src.isPlaying)
            {
                // Terminou de sumir: a exploração pausa (continua depois), menu e batalha param.
                if (ch.KeepPosition)
                {
                    src.Pause();
                    ch.Paused = true;
                }
                else src.Stop();
            }
        }
    }
}
