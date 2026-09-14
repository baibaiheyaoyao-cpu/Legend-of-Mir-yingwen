using Client.MirGraphics;
using Client.MirSounds;

namespace Client.MirObjects
{
    /// <summary>
    /// 服务端下发的自定义技能配置表(S.CustomSkillConfigs, 登录/面板保存时刷新).
    /// 施法动作/特效素材/音效全部按表渲染; 表为空或未配置特效则安全降级(无特效不报错).
    /// </summary>
    public static class CustomSkillSettings
    {
        private static readonly Dictionary<Spell, CustomSkillConfig> Configs = new Dictionary<Spell, CustomSkillConfig>();

        public static void Set(List<CustomSkillConfig> list)
        {
            Configs.Clear();
            if (list == null) return;
            foreach (var cfg in list)
                if (cfg != null && cfg.Enabled) Configs[cfg.Spell] = cfg;
        }

        public static CustomSkillConfig Get(Spell spell)
        {
            Configs.TryGetValue(spell, out var cfg);
            return cfg;
        }

        public static bool IsCustom(Spell spell)
        {
            return (byte)spell >= (byte)Spell.Custom1 && (byte)spell <= (byte)Spell.Custom14;
        }

        public static MLibrary EffectLibrary(byte lib)
        {
            switch (lib)
            {
                case 0: return Libraries.Magic;
                case 1: return Libraries.Magic2;
                case 2: return Libraries.Magic3;
                case 3: return Libraries.Magic4;
                case 4: return Libraries.MagicC;
                case 5: return Libraries.MagicD;
                default: return null;
            }
        }

        /// <summary>按配置生成贴身特效(跟随施法者, 带方向分组), 含音效</summary>
        public static void SpawnEffect(MapObject owner, Spell spell)
        {
            if (owner == null) return;
            CustomSkillConfig cfg = Get(spell);
            if (cfg == null || cfg.EffectLib > 5) return;

            MLibrary lib = EffectLibrary(cfg.EffectLib);
            if (lib == null || cfg.EffectCount <= 0) return;

            owner.Effects.Add(new DirectionalEffect(lib, cfg.EffectBase, cfg.EffectCount,
                Math.Max(300, (int)cfg.EffectDuration), owner, (int)owner.Direction, cfg.EffectStride));

            if (cfg.SoundSpell > 0)
                SoundManager.PlaySound(20000 + cfg.SoundSpell * 10);
        }
    }
}
