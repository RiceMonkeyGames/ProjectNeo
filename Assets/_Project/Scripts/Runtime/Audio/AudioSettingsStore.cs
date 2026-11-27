using System;
using System.IO;

namespace ProjectNeo.Audio
{
    /// <summary>
    /// 음량 설정을 폴더 안의 audio.json 하나에 저장하고 읽는다. 기본 폴더는 persistentDataPath/Settings이고
    /// 테스트는 임시 폴더를 넘긴다. 파일이 없거나 깨졌으면 기본값을 돌려주고 게임을 멈추지 않는다.
    /// </summary>
    public sealed class AudioSettingsStore
    {
        public const string FileName = "audio.json";

        public string Directory { get; }
        public string FilePath => Path.Combine(Directory, FileName);

        /// <summary>마지막 저장·읽기의 파일 오류. 없으면 빈 문자열.</summary>
        public string LastError { get; private set; } = string.Empty;

        public AudioSettingsStore(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Settings directory is required.", nameof(directory));
            Directory = directory;
        }

        /// <summary>설정을 읽는다. 없거나 읽을 수 없으면 기본값(LastError에 이유).</summary>
        public AudioSettingsData Load()
        {
            LastError = string.Empty;
            string path = FilePath;
            try
            {
                if (!File.Exists(path)) return new AudioSettingsData();
                AudioSettingsData data = AudioSettingsData.FromJson(File.ReadAllText(path));
                if (data != null) return data;
                LastError = "Unreadable audio settings file.";
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
            }
            return new AudioSettingsData();
        }

        /// <summary>임시 파일에 다 쓴 뒤 바꿔치기한다. 실패하면 false이고 기존 파일은 남는다.</summary>
        public bool Save(AudioSettingsData data)
        {
            LastError = string.Empty;
            if (data == null) return false;
            string path = FilePath, temp = path + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(temp, data.ToJson());
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                LastError = exception.Message;
                return false;
            }
        }

        static bool IsFileError(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException;
    }
}
