using System;
using System.Reflection;
using ProjectNeo.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// AUDIO-01 공용 믹서와 AudioDirector 설정 에셋을 만든다. 믹서에는 Master 아래 BGM·SFX·UI·Voice 통로를 두고
    /// 통로마다 음량을 AudioDirectorConfig.VolumeParameter 이름으로 노출한다. 이미 있는 믹서·통로·노출 값은 그대로 두므로
    /// 다시 실행해도 같은 결과다. Unity에는 믹서를 만드는 공개 API가 없어 에디터 내부 형식(AudioMixerController)을 리플렉션으로 쓴다.
    /// </summary>
    public static class AudioSetup
    {
        public const string MixerPath = "Assets/_Project/Audio/ProjectNeoMixer.mixer";
        public const string ConfigPath = "Assets/_Project/Resources/Audio/AudioDirectorConfig.asset";

        static readonly (AudioBus Bus, string Group)[] Buses =
        {
            (AudioBus.Bgm, "BGM"),
            (AudioBus.Sfx, "SFX"),
            (AudioBus.Ui, "UI"),
            (AudioBus.Voice, "Voice"),
        };

        const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [MenuItem("ProjectNeo/Setup/Audio Foundation (AUDIO-01)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");

            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type controllerType = Required(editor.GetType("UnityEditor.Audio.AudioMixerController"), "AudioMixerController");
            Type groupType = Required(editor.GetType("UnityEditor.Audio.AudioMixerGroupController"), "AudioMixerGroupController");
            Type pathType = Required(editor.GetType("UnityEditor.Audio.AudioGroupParameterPath"), "AudioGroupParameterPath");

            EnsureFolder("Assets/_Project/Audio");
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                MethodInfo create = Required(controllerType.GetMethod("CreateMixerControllerAtPath", AnyMember), "CreateMixerControllerAtPath");
                mixer = (AudioMixer)create.Invoke(null, new object[] { MixerPath });
                AssetDatabase.ImportAsset(MixerPath);
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            if (mixer == null) throw new InvalidOperationException("Could not create mixer: " + MixerPath);

            object master = Required(controllerType.GetProperty("masterGroup", AnyMember), "masterGroup").GetValue(mixer);
            var groups = new AudioMixerGroup[Enum.GetValues(typeof(AudioBus)).Length];
            groups[(int)AudioBus.Master] = (AudioMixerGroup)master;
            foreach ((AudioBus bus, string groupName) in Buses)
                groups[(int)bus] = FindOrCreateGroup(mixer, controllerType, master, groupName);

            int exposed = 0;
            foreach (AudioBus bus in (AudioBus[])Enum.GetValues(typeof(AudioBus)))
                if (Expose(mixer, controllerType, groupType, pathType, groups[(int)bus], AudioDirectorConfig.VolumeParameter(bus)))
                    exposed++;
            EditorUtility.SetDirty(mixer);

            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder("Assets/_Project/Resources/Audio");
            var config = AssetDatabase.LoadAssetAtPath<AudioDirectorConfig>(ConfigPath);
            if (config == null)
            {
                config = AudioDirectorConfig.Create();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var serialized = new SerializedObject(config);
            serialized.FindProperty("mixer").objectReferenceValue = mixer;
            serialized.FindProperty("masterGroup").objectReferenceValue = groups[(int)AudioBus.Master];
            serialized.FindProperty("bgmGroup").objectReferenceValue = groups[(int)AudioBus.Bgm];
            serialized.FindProperty("sfxGroup").objectReferenceValue = groups[(int)AudioBus.Sfx];
            serialized.FindProperty("uiGroup").objectReferenceValue = groups[(int)AudioBus.Ui];
            serialized.FindProperty("voiceGroup").objectReferenceValue = groups[(int)AudioBus.Voice];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return $"mixer={MixerPath} groups={mixer.FindMatchingGroups(string.Empty).Length} newlyExposed={exposed} config={ConfigPath}";
        }

        static AudioMixerGroup FindOrCreateGroup(AudioMixer mixer, Type controllerType, object master, string groupName)
        {
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(string.Empty))
                if (group.name == groupName) return group;
            MethodInfo create = Required(controllerType.GetMethod("CreateNewGroup", AnyMember), "CreateNewGroup");
            MethodInfo add = Required(controllerType.GetMethod("AddChildToParent", AnyMember), "AddChildToParent");
            // AddGroupToCurrentView는 새 믹서에 보기가 하나도 없으면 IndexOutOfRange를 던지므로 부르지 않는다.
            object created = create.Invoke(mixer, new object[] { groupName, false });
            add.Invoke(mixer, new[] { created, master });
            return (AudioMixerGroup)created;
        }

        /// <summary>통로 음량을 이름을 붙여 노출한다. 이미 그 이름이 있으면 false.</summary>
        static bool Expose(AudioMixer mixer, Type controllerType, Type groupType, Type pathType, AudioMixerGroup group, string parameter)
        {
            PropertyInfo exposedProperty = Required(controllerType.GetProperty("exposedParameters", AnyMember), "exposedParameters");
            var current = (Array)exposedProperty.GetValue(mixer);
            Type entryType = current.GetType().GetElementType();
            FieldInfo nameField = Required(entryType.GetField("name", AnyMember), "ExposedAudioParameter.name");
            FieldInfo guidField = Required(entryType.GetField("guid", AnyMember), "ExposedAudioParameter.guid");
            foreach (object entry in current)
                if ((string)nameField.GetValue(entry) == parameter) return false;

            var guid = (GUID)Required(groupType.GetMethod("GetGUIDForVolume", AnyMember), "GetGUIDForVolume").Invoke(group, null);
            object path = Activator.CreateInstance(pathType, group, guid);
            Required(controllerType.GetMethod("AddExposedParameter", AnyMember), "AddExposedParameter").Invoke(mixer, new[] { path });

            var updated = (Array)exposedProperty.GetValue(mixer);
            for (int i = 0; i < updated.Length; i++)
            {
                object entry = updated.GetValue(i);
                if (!guid.Equals(guidField.GetValue(entry))) continue;
                nameField.SetValue(entry, parameter);
                updated.SetValue(entry, i);
            }
            exposedProperty.SetValue(mixer, updated);
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static T Required<T>(T value, string what) where T : class =>
            value ?? throw new InvalidOperationException("Unity editor audio API changed; missing " + what + ".");
    }
}
