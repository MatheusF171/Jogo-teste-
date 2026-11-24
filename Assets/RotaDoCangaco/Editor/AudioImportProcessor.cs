using UnityEditor;
using UnityEngine;

namespace RotaDoCangaco.EditorTools
{
    /// <summary>
    /// Garante que as músicas de Resources/Audio sejam importadas em streaming (sem carregar a faixa inteira na memória),
    /// comprimidas em Vorbis com qualidade média.
    /// </summary>
    public class AudioImportProcessor : AssetPostprocessor
    {
        // Mudar a versão faz o Unity reimportar as músicas com a qualidade nova.
        public override uint GetVersion() => 4;

        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("RotaDoCangaco/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            // 32 kHz corta só o que fica acima de 16 kHz (as MP3 de 128 kbps já cortam ali) e deixa o
            // executável compartilhável (< 25 MB) sem baixar muito a qualidade da compressão.
            settings.quality = 0.5f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 32000;
            importer.defaultSampleSettings = settings;
        }
    }
}
