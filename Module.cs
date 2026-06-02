using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;

namespace CustomTranslator
{
    [Export(typeof(Module))]
    public class CustomTranslatorModule : Module
    {
        private Hook _textDrawHook;
        private readonly Dictionary<string, string> _translationDict = new Dictionary<string, string>();
        private string _mappingFilePath;
        private bool _isEnabled = false;

        [ImportingConstructor]
        public CustomTranslatorModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        // 模块初次加载（游戏启动或插件载入时）
        protected override void Initialize()
        {
            // 定义外部映射本的存放路径：Documents/Blish HUD/storage/custom_translator/mapping.txt
            _mappingFilePath = Path.Combine(DirectoriesManager.GetFullDirectoryPath("custom_translator"), "mapping.txt");
        }

        // 当用户在 Blish HUD 界面勾选“启用”时触发
        protected override async Task LoadAsync()
        {
            _isEnabled = true;
            
            LoadExternalMapping();
            if (_textDrawHook == null)
            {
                var originalMethod = typeof(SpriteBatchExtensions).GetMethod("DrawString", 
                    new[] { typeof(SpriteBatch), typeof(SpriteFont), typeof(string), typeof(Vector2), typeof(Color) });
                
                var targetMethod = typeof(CustomTranslatorModule).GetMethod("CustomDrawString", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (originalMethod != null && targetMethod != null)
                {
                    _textDrawHook = new Hook(originalMethod, targetMethod);
                }
            }
        }

        // 读取本地外部纯文本文件
        private void LoadExternalMapping()
        {
            if (!File.Exists(_mappingFilePath))
            {                
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(_mappingFilePath));
                    File.WriteAllText(_mappingFilePath, "# 在下方输入你的手动对照，格式为：英文=中文\n# 每次在Blish里重启本模块即可刷新\nVabbi=瓦比\n");
                } catch { }
                return;
            }

            _translationDict.Clear();
            try
            {
                foreach (var line in File.ReadAllLines(_mappingFilePath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || !trimmed.Contains("=")) 
                        continue; 
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        _translationDict[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }
            catch { }
        }

        // 全局渲染拦截代理静态
        public static void CustomDrawString(Action<SpriteBatch, SpriteFont, string, Vector2, Color> orig, 
            SpriteBatch spriteBatch, SpriteFont spriteFont, string text, Vector2 position, Color color)
        {
            var instance = Module.ModuleInstance as CustomTranslatorModule;

            if (instance != null && instance._isEnabled && !string.IsNullOrEmpty(text))
            {
                // 检查文本是否在 mapping.txt 里
                if (instance._translationDict.TryGetValue(text, out string chineseText))
                {
                    text = chineseText; 
                }
            }
            orig(spriteBatch, spriteFont, text, position, color);
        }

        // 当用户在 Blish HUD 界面取消勾选“禁用”时触发
        protected override void Unload()
        {
            _isEnabled = false;
            _textDrawHook?.Dispose();
            _textDrawHook = null;
            _translationDict.Clear();
        }
    }
}
