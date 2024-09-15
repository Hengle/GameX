using GameX.WB.Formats.AC.Entity;
using GameX.WB.Formats.AC.Props;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace GameX.WB.Formats.AC.Models
{
    #region Biota

    /// <summary>
    /// Only populated collections and dictionaries are initialized.
    /// We do this to conserve memory in ACE.Server
    /// Be sure to check for null first.
    /// </summary>
    public class Biota : IWeenie
    {
        public uint Id { get; set; }
        public uint WeenieClassId { get; set; }
        public WeenieType WeenieType { get; set; }

        public IDictionary<PropertyBool, bool> PropertiesBool { get; set; }
        public IDictionary<PropertyDataId, uint> PropertiesDID { get; set; }
        public IDictionary<PropertyFloat, double> PropertiesFloat { get; set; }
        public IDictionary<PropertyInstanceId, uint> PropertiesIID { get; set; }
        public IDictionary<PropertyInt, int> PropertiesInt { get; set; }
        public IDictionary<PropertyInt64, long> PropertiesInt64 { get; set; }
        public IDictionary<PropertyString, string> PropertiesString { get; set; }

        public IDictionary<PositionType, PropertiesPosition> PropertiesPosition { get; set; }

        public IDictionary<int, float /* probability */> PropertiesSpellBook { get; set; }

        public IList<PropertiesAnimPart> PropertiesAnimPart { get; set; }
        public IList<PropertiesPalette> PropertiesPalette { get; set; }
        public IList<PropertiesTextureMap> PropertiesTextureMap { get; set; }

        // Properties for all world objects that typically aren't modified over the original weenie
        public ICollection<PropertiesCreateList> PropertiesCreateList { get; set; }
        public ICollection<PropertiesEmote> PropertiesEmote { get; set; }
        public HashSet<int> PropertiesEventFilter { get; set; }
        public IList<PropertiesGenerator> PropertiesGenerator { get; set; }

        // Properties for creatures
        public IDictionary<PropertyAttribute, PropertiesAttribute> PropertiesAttribute { get; set; }
        public IDictionary<PropertyAttribute2nd, PropertiesAttribute2nd> PropertiesAttribute2nd { get; set; }
        public IDictionary<CombatBodyPart, PropertiesBodyPart> PropertiesBodyPart { get; set; }
        public IDictionary<Skill, PropertiesSkill> PropertiesSkill { get; set; }

        // Properties for books
        public PropertiesBook PropertiesBook { get; set; }
        public IList<PropertiesBookPageData> PropertiesBookPageData { get; set; }

        // Biota additions over Weenie
        public IDictionary<uint /* Character ID */, PropertiesAllegiance> PropertiesAllegiance { get; set; }
        public ICollection<PropertiesEnchantmentRegistry> PropertiesEnchantmentRegistry { get; set; }
        public IDictionary<uint /* Player GUID */, bool /* Storage */> HousePermissions { get; set; }
    }

    public static class BiotaExtensions
    {
        // =====================================
        // Get
        // Bool, DID, Float, IID, Int, Int64, String, Position
        // =====================================

        public static bool? GetProperty(this Biota biota, PropertyBool property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesBool == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesBool.TryGetValue(property, out var value) ? (bool?)value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static uint? GetProperty(this Biota biota, PropertyDataId property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesDID == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesDID.TryGetValue(property, out var value) ? (uint?)value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static double? GetProperty(this Biota biota, PropertyFloat property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesFloat == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesFloat.TryGetValue(property, out var value) ? (double?)value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static uint? GetProperty(this Biota biota, PropertyInstanceId property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesIID == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesIID.TryGetValue(property, out var value) ? (uint?)value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static int? GetProperty(this Biota biota, PropertyInt property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesInt == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesInt.TryGetValue(property, out var value) ? (int?)value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static long? GetProperty(this Biota biota, PropertyInt64 property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesInt64 == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesInt64.TryGetValue(property, out var value) ? (long?)value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static string GetProperty(this Biota biota, PropertyString property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesString == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesString.TryGetValue(property, out var value) ? value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static PropertiesPosition GetProperty(this Biota biota, PositionType property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesPosition == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesPosition.TryGetValue(property, out var value) ? value : null; }
            finally { rwLock.ExitReadLock(); }
        }

        public static XPosition GetPosition(this Biota biota, PositionType property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesPosition == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesPosition.TryGetValue(property, out var value) ? new XPosition(value.ObjCellId, value.PositionX, value.PositionY, value.PositionZ, value.RotationX, value.RotationY, value.RotationZ, value.RotationW) : null; }
            finally { rwLock.ExitReadLock(); }
        }


        // =====================================
        // Set
        // Bool, DID, Float, IID, Int, Int64, String, Position
        // =====================================

        public static void SetProperty(this Biota biota, PropertyBool property, bool value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesBool ??= new Dictionary<PropertyBool, bool>();
                changed = !biota.PropertiesBool.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesBool[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PropertyDataId property, uint value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesDID ??= new Dictionary<PropertyDataId, uint>();
                changed = !biota.PropertiesDID.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesDID[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PropertyFloat property, double value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesFloat ??= new Dictionary<PropertyFloat, double>();
                changed = !biota.PropertiesFloat.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesFloat[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PropertyInstanceId property, uint value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesIID ??= new Dictionary<PropertyInstanceId, uint>();
                changed = !biota.PropertiesIID.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesIID[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PropertyInt property, int value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesInt ??= new Dictionary<PropertyInt, int>();
                changed = !biota.PropertiesInt.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesInt[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PropertyInt64 property, long value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesInt64 ??= new Dictionary<PropertyInt64, long>();
                changed = !biota.PropertiesInt64.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesInt64[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PropertyString property, string value, ReaderWriterLockSlim rwLock, out bool changed)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesString ??= new Dictionary<PropertyString, string>();
                changed = !biota.PropertiesString.TryGetValue(property, out var existing) || value != existing;
                if (changed) biota.PropertiesString[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetProperty(this Biota biota, PositionType property, PropertiesPosition value, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesPosition ??= new Dictionary<PositionType, PropertiesPosition>();
                biota.PropertiesPosition[property] = value;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void SetPosition(this Biota biota, PositionType property, XPosition value, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try
            {
                biota.PropertiesPosition ??= new Dictionary<PositionType, PropertiesPosition>();
                var entity = new PropertiesPosition { ObjCellId = value.Cell, PositionX = value.PositionX, PositionY = value.PositionY, PositionZ = value.PositionZ, RotationW = value.RotationW, RotationX = value.RotationX, RotationY = value.RotationY, RotationZ = value.RotationZ };
                biota.PropertiesPosition[property] = entity;
            }
            finally { rwLock.ExitWriteLock(); }
        }


        // =====================================
        // Remove
        // Bool, DID, Float, IID, Int, Int64, String, Position
        // =====================================

        public static bool TryRemoveProperty(this Biota biota, PropertyBool property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesBool == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesBool.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PropertyDataId property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesDID == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesDID.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PropertyFloat property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesFloat == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesFloat.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PropertyInstanceId property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesIID == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesIID.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PropertyInt property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesInt == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesInt.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PropertyInt64 property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesInt64 == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesInt64.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PropertyString property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesString == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesString.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveProperty(this Biota biota, PositionType property, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesPosition == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesPosition.Remove(property); }
            finally { rwLock.ExitWriteLock(); }
        }


        // =====================================
        // BiotaPropertiesSpellBook
        // =====================================

        public static Dictionary<int, float> CloneSpells(this Biota biota, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return [];
            rwLock.EnterReadLock();
            try
            {
                var results = new Dictionary<int, float>();
                foreach (var kvp in biota.PropertiesSpellBook) results[kvp.Key] = kvp.Value;
                return results;
            }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool HasKnownSpell(this Biota biota, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return false;
            rwLock.EnterReadLock();
            try { return biota.PropertiesSpellBook.Count > 0; }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<int> GetKnownSpellsIds(this Biota biota, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return [];
            rwLock.EnterReadLock();
            try { return biota.PropertiesSpellBook == null ? [] : new List<int>(biota.PropertiesSpellBook.Keys); }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<int> GetKnownSpellsIdsWhere(this Biota biota, Func<int, bool> predicate, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return [];
            rwLock.EnterReadLock();
            try { return biota.PropertiesSpellBook == null ? [] : biota.PropertiesSpellBook.Keys.Where(predicate).ToList(); }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<float> GetKnownSpellsProbabilities(this Biota biota, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return [];
            rwLock.EnterReadLock();
            try { return biota.PropertiesSpellBook == null ? [] : new List<float>(biota.PropertiesSpellBook.Values); }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool SpellIsKnown(this Biota biota, int spell, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return false;
            rwLock.EnterReadLock();
            try { return biota.PropertiesSpellBook.ContainsKey(spell); }
            finally { rwLock.ExitReadLock(); }
        }

        public static float GetOrAddKnownSpell(this Biota biota, int spell, ReaderWriterLockSlim rwLock, out bool spellAdded, float probability = 2.0f)
        {
            rwLock.EnterWriteLock();
            try
            {
                if (biota.PropertiesSpellBook != null && biota.PropertiesSpellBook.TryGetValue(spell, out var value)) { spellAdded = false; return value; }
                biota.PropertiesSpellBook ??= new Dictionary<int, float>();
                biota.PropertiesSpellBook[spell] = probability;
                spellAdded = true;
                return probability;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static Dictionary<int, float> GetMatchingSpells(this Biota biota, HashSet<int> match, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return new Dictionary<int, float>();
            rwLock.EnterReadLock();
            try
            {
                var results = new Dictionary<int, float>();
                foreach (var value in biota.PropertiesSpellBook) if (match.Contains(value.Key)) results[value.Key] = value.Value;
                return results;
            }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool TryRemoveKnownSpell(this Biota biota, int spell, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return false;
            rwLock.EnterWriteLock();
            try { return biota.PropertiesSpellBook.Remove(spell); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void ClearSpells(this Biota biota, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSpellBook == null) return;
            rwLock.EnterWriteLock();
            try { biota.PropertiesSpellBook?.Clear(); }
            finally { rwLock.ExitWriteLock(); }
        }


        // =====================================
        // BiotaPropertiesSkill
        // =====================================

        public static PropertiesSkill GetSkill(this Biota biota, Skill skill, ReaderWriterLockSlim rwLock)
        {
            if (biota.PropertiesSkill == null) return null;
            rwLock.EnterReadLock();
            try { return biota.PropertiesSkill == null || !biota.PropertiesSkill.TryGetValue(skill, out var value) ? null : value; }
            finally { rwLock.ExitReadLock(); }
        }

        public static PropertiesSkill GetOrAddSkill(this Biota biota, Skill skill, ReaderWriterLockSlim rwLock, out bool skillAdded)
        {
            rwLock.EnterWriteLock();
            try
            {
                if (biota.PropertiesSkill != null && biota.PropertiesSkill.TryGetValue(skill, out var value)) { skillAdded = false; return value; }
                biota.PropertiesSkill ??= new Dictionary<Skill, PropertiesSkill>();
                var entity = new PropertiesSkill();
                biota.PropertiesSkill[skill] = entity;
                skillAdded = true;
                return entity;
            }
            finally { rwLock.ExitWriteLock(); }
        }


        // =====================================
        // HousePermissions
        // =====================================

        public static Dictionary<uint, bool> CloneHousePermissions(this Biota biota, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterReadLock();
            try { return biota.HousePermissions == null ? [] : new Dictionary<uint, bool>(biota.HousePermissions); }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool HasHouseGuest(this Biota biota, uint guestGuid, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterReadLock();
            try { return biota.HousePermissions == null ? false : biota.HousePermissions.ContainsKey(guestGuid); }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool? GetHouseGuestStoragePermission(this Biota biota, uint guestGuid, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterReadLock();
            try { return biota.HousePermissions == null || !biota.HousePermissions.TryGetValue(guestGuid, out var value) ? null : (bool?)value; }
            finally { rwLock.ExitReadLock(); }
        }

        public static void AddOrUpdateHouseGuest(this Biota biota, uint guestGuid, bool storage, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try
            {
                if (biota.HousePermissions == null) biota.HousePermissions = new Dictionary<uint, bool>();
                biota.HousePermissions[guestGuid] = storage;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool RemoveHouseGuest(this Biota biota, uint guestGuid, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try { return biota.HousePermissions == null ? false : biota.HousePermissions.Remove(guestGuid); }
            finally { rwLock.ExitWriteLock(); }
        }


        // =====================================
        // Utility
        // =====================================

        public static string GetName(this Biota biota) => biota.PropertiesString.TryGetValue(PropertyString.Name, out var value) ? value : null;
    }

    #endregion

    #region Properties

    public static partial class PropertiesExtensions { }

    public class PropertiesAllegiance
    {
        public bool Banned { get; set; }
        public bool ApprovedVassal { get; set; }
    }

    partial class PropertiesExtensions
    {
        public static Dictionary<uint, PropertiesAllegiance> GetApprovedVassals(this IDictionary<uint, PropertiesAllegiance> value, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterReadLock();
            if (value == null) return [];
            try { return value.Where(i => i.Value.ApprovedVassal).ToDictionary(i => i.Key, i => i.Value); }
            finally { rwLock.ExitReadLock(); }
        }

        public static Dictionary<uint, PropertiesAllegiance> GetBanList(this IDictionary<uint, PropertiesAllegiance> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return [];
            rwLock.EnterReadLock();
            try { return value.Where(i => i.Value.Banned).ToDictionary(i => i.Key, i => i.Value); }
            finally { rwLock.ExitReadLock(); }
        }

        public static PropertiesAllegiance GetFirstOrDefaultByCharacterId(this IDictionary<uint, PropertiesAllegiance> value, uint characterId, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { value.TryGetValue(characterId, out var entity); return entity; }
            finally { rwLock.ExitReadLock(); }
        }

        public static void AddOrUpdateAllegiance(this IDictionary<uint, PropertiesAllegiance> value, uint characterId, bool isBanned, bool approvedVassal, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try
            {
                if (!value.TryGetValue(characterId, out var entity)) { entity = new PropertiesAllegiance { Banned = isBanned, ApprovedVassal = approvedVassal }; value.Add(characterId, entity); }
                entity.Banned = isBanned;
                entity.ApprovedVassal = approvedVassal;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveAllegiance(this IDictionary<uint, PropertiesAllegiance> value, uint characterId, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return false;
            rwLock.EnterWriteLock();
            try { return value.Remove(characterId); }
            finally { rwLock.ExitWriteLock(); }
        }
    }

    public class PropertiesAnimPart
    {
        public byte Index { get; set; }
        public uint AnimationId { get; set; }

        public PropertiesAnimPart Clone() => new()
        {
            Index = Index,
            AnimationId = AnimationId
        };
    }

    partial class PropertiesExtensions
    {
        public static int GetCount(this IList<PropertiesAnimPart> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return 0;
            rwLock.EnterReadLock();
            try { return value.Count; }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesAnimPart> Clone(this IList<PropertiesAnimPart> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return new List<PropertiesAnimPart>(value); }
            finally { rwLock.ExitReadLock(); }
        }

        public static void CopyTo(this IList<PropertiesAnimPart> value, ICollection<PropertiesAnimPart> destination, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return;
            rwLock.EnterReadLock();
            try { foreach (var entry in value) destination.Add(entry); }
            finally { rwLock.ExitReadLock(); }
        }
    }

    public class PropertiesAttribute
    {
        public uint InitLevel { get; set; }
        public uint LevelFromCP { get; set; }
        public uint CPSpent { get; set; }

        public PropertiesAttribute Clone() => new PropertiesAttribute
        {
            InitLevel = InitLevel,
            LevelFromCP = LevelFromCP,
            CPSpent = CPSpent
        };
    }

    public class PropertiesAttribute2nd
    {
        public uint InitLevel { get; set; }
        public uint LevelFromCP { get; set; }
        public uint CPSpent { get; set; }
        public uint CurrentLevel { get; set; }

        public PropertiesAttribute2nd Clone() => new PropertiesAttribute2nd
        {
            InitLevel = InitLevel,
            LevelFromCP = LevelFromCP,
            CPSpent = CPSpent,
            CurrentLevel = CurrentLevel,
        };
    }

    public class PropertiesBodyPart
    {
        public DamageType DType { get; set; }
        public int DVal { get; set; }
        public float DVar { get; set; }
        public int BaseArmor { get; set; }
        public int ArmorVsSlash { get; set; }
        public int ArmorVsPierce { get; set; }
        public int ArmorVsBludgeon { get; set; }
        public int ArmorVsCold { get; set; }
        public int ArmorVsFire { get; set; }
        public int ArmorVsAcid { get; set; }
        public int ArmorVsElectric { get; set; }
        public int ArmorVsNether { get; set; }
        public int BH { get; set; }
        public float HLF { get; set; }
        public float MLF { get; set; }
        public float LLF { get; set; }
        public float HRF { get; set; }
        public float MRF { get; set; }
        public float LRF { get; set; }
        public float HLB { get; set; }
        public float MLB { get; set; }
        public float LLB { get; set; }
        public float HRB { get; set; }
        public float MRB { get; set; }
        public float LRB { get; set; }

        public PropertiesBodyPart Clone() => new()
        {
            DType = DType,
            DVal = DVal,
            DVar = DVar,
            BaseArmor = BaseArmor,
            ArmorVsSlash = ArmorVsSlash,
            ArmorVsPierce = ArmorVsPierce,
            ArmorVsBludgeon = ArmorVsBludgeon,
            ArmorVsCold = ArmorVsCold,
            ArmorVsFire = ArmorVsFire,
            ArmorVsAcid = ArmorVsAcid,
            ArmorVsElectric = ArmorVsElectric,
            ArmorVsNether = ArmorVsNether,
            BH = BH,
            HLF = HLF,
            MLF = MLF,
            LLF = LLF,
            HRF = HRF,
            MRF = MRF,
            LRF = LRF,
            HLB = HLB,
            MLB = MLB,
            LLB = LLB,
            HRB = HRB,
            MRB = MRB,
            LRB = LRB,
        };
    }

    public class PropertiesBook
    {
        public int MaxNumPages { get; set; }
        public int MaxNumCharsPerPage { get; set; }

        public PropertiesBook Clone() => new()
        {
            MaxNumPages = MaxNumPages,
            MaxNumCharsPerPage = MaxNumCharsPerPage,
        };
    }

    public class PropertiesBookPageData
    {
        public uint AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorAccount { get; set; }
        public bool IgnoreAuthor { get; set; }
        public string PageText { get; set; }

        public PropertiesBookPageData Clone() => new()
        {
            AuthorId = AuthorId,
            AuthorName = AuthorName,
            AuthorAccount = AuthorAccount,
            IgnoreAuthor = IgnoreAuthor,
            PageText = PageText,
        };
    }

    partial class PropertiesExtensions
    {
        public static int GetPageCount(this IList<PropertiesBookPageData> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return 0;
            rwLock.EnterReadLock();
            try { return value.Count; }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesBookPageData> Clone(this IList<PropertiesBookPageData> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return new List<PropertiesBookPageData>(value); }
            finally { rwLock.ExitReadLock(); }
        }

        public static PropertiesBookPageData GetPage(this IList<PropertiesBookPageData> value, int index, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return value.Count <= index ? null : value[index]; }
            finally { rwLock.ExitReadLock(); }
        }

        public static void AddPage(this IList<PropertiesBookPageData> value, PropertiesBookPageData page, out int index, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try { value.Add(page); index = value.Count; }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool RemovePage(this IList<PropertiesBookPageData> value, int index, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return false;
            rwLock.EnterWriteLock();
            try { if (value.Count <= index) return false; value.RemoveAt(index); return true; }
            finally { rwLock.ExitWriteLock(); }
        }
    }

    public class PropertiesCreateList
    {
        /// <summary>
        /// This is only used to tie this property back to a specific database row
        /// </summary>
        public uint DatabaseRecordId { get; set; }

        public DestinationType DestinationType { get; set; }
        public uint WeenieClassId { get; set; }
        public int StackSize { get; set; }
        public sbyte Palette { get; set; }
        public float Shade { get; set; }
        public bool TryToBond { get; set; }

        public PropertiesCreateList Clone() => new()
        {
            DestinationType = DestinationType,
            WeenieClassId = WeenieClassId,
            StackSize = StackSize,
            Palette = Palette,
            Shade = Shade,
            TryToBond = TryToBond,
        };
    }

    public class PropertiesEmote
    {
        /// <summary>
        /// This is only used to tie this property back to a specific database row
        /// </summary>
        public uint DatabaseRecordId { get; set; }

        public EmoteCategory Category { get; set; }
        public float Probability { get; set; }
        public uint? WeenieClassId { get; set; }
        public MotionStance? Style { get; set; }
        public MotionCommand? Substyle { get; set; }
        public string Quest { get; set; }
        public VendorType? VendorType { get; set; }
        public float? MinHealth { get; set; }
        public float? MaxHealth { get; set; }

        public Weenie Object { get; set; }
        public IList<PropertiesEmoteAction> PropertiesEmoteAction { get; set; } = [];

        public PropertiesEmote Clone()
        {
            var result = new PropertiesEmote
            {
                Category = Category,
                Probability = Probability,
                WeenieClassId = WeenieClassId,
                Style = Style,
                Substyle = Substyle,
                Quest = Quest,
                VendorType = VendorType,
                MinHealth = MinHealth,
                MaxHealth = MaxHealth,
            };
            foreach (var action in PropertiesEmoteAction) result.PropertiesEmoteAction.Add(action.Clone());
            return result;
        }
    }

    public class PropertiesEmoteAction
    {
        /// <summary>
        /// This is only used to tie this property back to a specific database row
        /// </summary>
        public uint DatabaseRecordId { get; set; }

        public uint Type { get; set; }
        public float Delay { get; set; }
        public float Extent { get; set; }
        public MotionCommand? Motion { get; set; }
        public string Message { get; set; }
        public string TestString { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
        public long? Min64 { get; set; }
        public long? Max64 { get; set; }
        public double? MinDbl { get; set; }
        public double? MaxDbl { get; set; }
        public int? Stat { get; set; }
        public bool? Display { get; set; }
        public int? Amount { get; set; }
        public long? Amount64 { get; set; }
        public long? HeroXP64 { get; set; }
        public double? Percent { get; set; }
        public int? SpellId { get; set; }
        public int? WealthRating { get; set; }
        public int? TreasureClass { get; set; }
        public int? TreasureType { get; set; }
        public PlayScript? PScript { get; set; }
        public Sound? Sound { get; set; }
        public sbyte? DestinationType { get; set; }
        public uint? WeenieClassId { get; set; }
        public int? StackSize { get; set; }
        public int? Palette { get; set; }
        public float? Shade { get; set; }
        public bool? TryToBond { get; set; }
        public uint? ObjCellId { get; set; }
        public float? OriginX { get; set; }
        public float? OriginY { get; set; }
        public float? OriginZ { get; set; }
        public float? AnglesW { get; set; }
        public float? AnglesX { get; set; }
        public float? AnglesY { get; set; }
        public float? AnglesZ { get; set; }

        public PropertiesEmoteAction Clone() => new()
        {
            Type = Type,
            Delay = Delay,
            Extent = Extent,
            Motion = Motion,
            Message = Message,
            TestString = TestString,
            Min = Min,
            Max = Max,
            Min64 = Min64,
            Max64 = Max64,
            MinDbl = MinDbl,
            MaxDbl = MaxDbl,
            Stat = Stat,
            Display = Display,
            Amount = Amount,
            Amount64 = Amount64,
            HeroXP64 = HeroXP64,
            Percent = Percent,
            SpellId = SpellId,
            WealthRating = WealthRating,
            TreasureClass = TreasureClass,
            TreasureType = TreasureType,
            PScript = PScript,
            Sound = Sound,
            DestinationType = DestinationType,
            WeenieClassId = WeenieClassId,
            StackSize = StackSize,
            Palette = Palette,
            Shade = Shade,
            TryToBond = TryToBond,
            ObjCellId = ObjCellId,
            OriginX = OriginX,
            OriginY = OriginY,
            OriginZ = OriginZ,
            AnglesW = AnglesW,
            AnglesX = AnglesX,
            AnglesY = AnglesY,
            AnglesZ = AnglesZ,
        };
    }

    public class PropertiesEnchantmentRegistry
    {
        public uint EnchantmentCategory { get; set; }
        public int SpellId { get; set; }
        public ushort LayerId { get; set; }
        public bool HasSpellSetId { get; set; }
        public SpellCategory SpellCategory { get; set; }
        public uint PowerLevel { get; set; }
        public double StartTime { get; set; }
        public double Duration { get; set; }
        public uint CasterObjectId { get; set; }
        public float DegradeModifier { get; set; }
        public float DegradeLimit { get; set; }
        public double LastTimeDegraded { get; set; }
        public EnchantmentTypeFlags StatModType { get; set; }
        public uint StatModKey { get; set; }
        public float StatModValue { get; set; }
        public EquipmentSet SpellSetId { get; set; }
    }

    partial class PropertiesExtensions
    {
        public static List<PropertiesEnchantmentRegistry> Clone(this ICollection<PropertiesEnchantmentRegistry> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;

            rwLock.EnterReadLock();
            try { return value.ToList(); }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool HasEnchantments(this ICollection<PropertiesEnchantmentRegistry> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return false;
            rwLock.EnterReadLock();
            try { return value.Any(); }
            finally { rwLock.ExitReadLock(); }
        }

        public static bool HasEnchantment(this ICollection<PropertiesEnchantmentRegistry> value, uint spellId, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return false;
            rwLock.EnterReadLock();
            try { return value.Any(e => e.SpellId == spellId); }
            finally { rwLock.ExitReadLock(); }
        }

        public static PropertiesEnchantmentRegistry GetEnchantmentBySpell(this ICollection<PropertiesEnchantmentRegistry> value, int spellId, uint? casterGuid, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try
            {
                var results = value.Where(e => e.SpellId == spellId);
                if (casterGuid != null) results = results.Where(e => e.CasterObjectId == casterGuid);
                return results.FirstOrDefault();
            }
            finally { rwLock.ExitReadLock(); }
        }

        public static PropertiesEnchantmentRegistry GetEnchantmentBySpellSet(this ICollection<PropertiesEnchantmentRegistry> value, int spellId, EquipmentSet spellSetId, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return value.FirstOrDefault(e => e.SpellId == spellId && e.SpellSetId == spellSetId); }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesEnchantmentRegistry> GetEnchantmentsByCategory(this ICollection<PropertiesEnchantmentRegistry> value, SpellCategory spellCategory, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return value.Where(e => e.SpellCategory == spellCategory).ToList(); }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesEnchantmentRegistry> GetEnchantmentsByStatModType(this ICollection<PropertiesEnchantmentRegistry> value, EnchantmentTypeFlags statModType, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return value.Where(e => (e.StatModType & statModType) == statModType).ToList(); }
            finally { rwLock.ExitReadLock(); }
        }

        // this ensures level 8 item self spells always take precedence over level 8 item other spells
        private static HashSet<int> Level8AuraSelfSpells = new HashSet<int>
        {
            (int)SpellId.BloodDrinkerSelf8,
            (int)SpellId.DefenderSelf8,
            (int)SpellId.HeartSeekerSelf8,
            (int)SpellId.SpiritDrinkerSelf8,
            (int)SpellId.SwiftKillerSelf8,
            (int)SpellId.HermeticLinkSelf8,
        };

        public static List<PropertiesEnchantmentRegistry> GetEnchantmentsTopLayer(this ICollection<PropertiesEnchantmentRegistry> value, ReaderWriterLockSlim rwLock, HashSet<int> setSpells)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try
            {
                var results =
                    from e in value
                    group e by e.SpellCategory
                    into categories
                    //select categories.OrderByDescending(c => c.LayerId).First();
                    select categories.OrderByDescending(c => c.PowerLevel)
                        .ThenByDescending(c => Level8AuraSelfSpells.Contains(c.SpellId))
                        .ThenByDescending(c => setSpells.Contains(c.SpellId) ? c.SpellId : c.StartTime).First();
                return results.ToList();
            }
            finally { rwLock.ExitReadLock(); }
        }

        /// <summary>
        /// Returns the top layers in each spell category for a StatMod type
        /// </summary>
        public static List<PropertiesEnchantmentRegistry> GetEnchantmentsTopLayerByStatModType(this ICollection<PropertiesEnchantmentRegistry> value, EnchantmentTypeFlags statModType, ReaderWriterLockSlim rwLock, HashSet<int> setSpells)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try
            {
                var valuesByStatModType = value.Where(e => (e.StatModType & statModType) == statModType);
                var results =
                    from e in valuesByStatModType
                    group e by e.SpellCategory
                    into categories
                    //select categories.OrderByDescending(c => c.LayerId).First();
                    select categories.OrderByDescending(c => c.PowerLevel)
                        .ThenByDescending(c => Level8AuraSelfSpells.Contains(c.SpellId))
                        .ThenByDescending(c => setSpells.Contains(c.SpellId) ? c.SpellId : c.StartTime).First();
                return results.ToList();
            }
            finally { rwLock.ExitReadLock(); }
        }

        /// <summary>
        /// Returns the top layers in each spell category for a StatMod type + key
        /// </summary>
        public static List<PropertiesEnchantmentRegistry> GetEnchantmentsTopLayerByStatModType(this ICollection<PropertiesEnchantmentRegistry> value, EnchantmentTypeFlags statModType, uint statModKey, ReaderWriterLockSlim rwLock, HashSet<int> setSpells, bool handleMultiple = false)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try
            {
                var multipleStat = EnchantmentTypeFlags.Undef;
                if (handleMultiple)
                {
                    // todo: this is starting to get a bit messy here, EnchantmentTypeFlags handling should be more adaptable
                    // perhaps the enchantment registry in acclient should be investigated for reference logic
                    multipleStat = statModType | EnchantmentTypeFlags.MultipleStat;
                    statModType |= EnchantmentTypeFlags.SingleStat;
                }
                var valuesByStatModTypeAndKey = value.Where(e => (e.StatModType & statModType) == statModType && e.StatModKey == statModKey || (handleMultiple && (e.StatModType & multipleStat) == multipleStat && (e.StatModType & EnchantmentTypeFlags.Vitae) == 0 && e.StatModKey == 0));
                // 3rd spell id sort added for Gauntlet Damage Boost I / Gauntlet Damage Boost II, which is contained in multiple sets, and can overlap
                // without this sorting criteria, it's already matched up to the client, but produces logically incorrect results for server spell stacking
                // confirmed this bug still exists in acclient Enchantment.Duel(), unknown if it existed in retail server
                var results =
                    from e in valuesByStatModTypeAndKey
                    group e by e.SpellCategory
                    into categories
                    //select categories.OrderByDescending(c => c.LayerId).First();
                    select categories.OrderByDescending(c => c.PowerLevel)
                        .ThenByDescending(c => Level8AuraSelfSpells.Contains(c.SpellId))
                        .ThenByDescending(c => setSpells.Contains(c.SpellId) ? c.SpellId : c.StartTime).First();
                return results.ToList();
            }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesEnchantmentRegistry> HeartBeatEnchantmentsAndReturnExpired(this ICollection<PropertiesEnchantmentRegistry> value, double heartbeatInterval, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try
            {
                var expired = new List<PropertiesEnchantmentRegistry>();
                foreach (var enchantment in value)
                {
                    enchantment.StartTime -= heartbeatInterval;
                    // StartTime ticks backwards to -Duration
                    if (enchantment.Duration >= 0 && enchantment.StartTime <= -enchantment.Duration) expired.Add(enchantment);
                }
                return expired;
            }
            finally { rwLock.ExitReadLock(); }
        }

        public static void AddEnchantment(this ICollection<PropertiesEnchantmentRegistry> value, PropertiesEnchantmentRegistry entity, ReaderWriterLockSlim rwLock)
        {
            rwLock.EnterWriteLock();
            try { value.Add(entity); }
            finally { rwLock.ExitWriteLock(); }
        }

        public static bool TryRemoveEnchantment(this ICollection<PropertiesEnchantmentRegistry> value, int spellId, uint casterObjectId, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return false;
            rwLock.EnterWriteLock();
            try
            {
                var entity = value.FirstOrDefault(x => x.SpellId == spellId && x.CasterObjectId == casterObjectId);
                if (entity != null) { value.Remove(entity); return true; }
                return false;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public static void RemoveAllEnchantments(this ICollection<PropertiesEnchantmentRegistry> value, IEnumerable<int> spellsToExclude, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return;
            rwLock.EnterWriteLock();
            try
            {
                var enchantments = value.Where(e => !spellsToExclude.Contains(e.SpellId)).ToList();
                foreach (var enchantment in enchantments) value.Remove(enchantment);
            }
            finally { rwLock.ExitWriteLock(); }
        }
    }

    public class PropertiesGenerator
    {
        /// <summary>
        /// This is only used to tie this property back to a specific database row
        /// </summary>
        public uint DatabaseRecordId { get; set; }

        public float Probability { get; set; }
        public uint WeenieClassId { get; set; }
        public float? Delay { get; set; }
        public int InitCreate { get; set; }
        public int MaxCreate { get; set; }
        public RegenerationType WhenCreate { get; set; }
        public RegenLocationType WhereCreate { get; set; }
        public int? StackSize { get; set; }
        public uint? PaletteId { get; set; }
        public float? Shade { get; set; }
        public uint? ObjCellId { get; set; }
        public float? OriginX { get; set; }
        public float? OriginY { get; set; }
        public float? OriginZ { get; set; }
        public float? AnglesW { get; set; }
        public float? AnglesX { get; set; }
        public float? AnglesY { get; set; }
        public float? AnglesZ { get; set; }

        public PropertiesGenerator Clone() => new()
        {
            Probability = Probability,
            WeenieClassId = WeenieClassId,
            Delay = Delay,
            InitCreate = InitCreate,
            MaxCreate = MaxCreate,
            WhenCreate = WhenCreate,
            WhereCreate = WhereCreate,
            StackSize = StackSize,
            PaletteId = PaletteId,
            Shade = Shade,
            ObjCellId = ObjCellId,
            OriginX = OriginX,
            OriginY = OriginY,
            OriginZ = OriginZ,
            AnglesW = AnglesW,
            AnglesX = AnglesX,
            AnglesY = AnglesY,
            AnglesZ = AnglesZ,
        };
    }

    public class PropertiesPalette
    {
        public uint SubPaletteId { get; set; }
        public ushort Offset { get; set; }
        public ushort Length { get; set; }

        public PropertiesPalette Clone() => new PropertiesPalette
        {
            SubPaletteId = SubPaletteId,
            Offset = Offset,
            Length = Length,
        };
    }

    partial class PropertiesExtensions
    {
        public static int GetCount(this IList<PropertiesPalette> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return 0;
            rwLock.EnterReadLock();
            try { return value.Count; }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesPalette> Clone(this IList<PropertiesPalette> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return new List<PropertiesPalette>(value); }
            finally { rwLock.ExitReadLock(); }
        }

        public static void CopyTo(this IList<PropertiesPalette> value, ICollection<PropertiesPalette> destination, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return;
            rwLock.EnterReadLock();
            try { foreach (var entry in value) destination.Add(entry); }
            finally { rwLock.ExitReadLock(); }
        }
    }

    public class PropertiesPosition
    {
        public uint ObjCellId { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float RotationW { get; set; }
        public float RotationX { get; set; }
        public float RotationY { get; set; }
        public float RotationZ { get; set; }

        public PropertiesPosition Clone() => new()
        {
            ObjCellId = ObjCellId,
            PositionX = PositionX,
            PositionY = PositionY,
            PositionZ = PositionZ,
            RotationW = RotationW,
            RotationX = RotationX,
            RotationY = RotationY,
            RotationZ = RotationZ,
        };
    }

    public class PropertiesSkill
    {
        public ushort LevelFromPP { get; set; }
        public SkillAdvancementClass SAC { get; set; }
        public uint PP { get; set; }
        public uint InitLevel { get; set; }
        public uint ResistanceAtLastCheck { get; set; }
        public double LastUsedTime { get; set; }

        public PropertiesSkill Clone() => new PropertiesSkill
        {
            LevelFromPP = LevelFromPP,
            SAC = SAC,
            PP = PP,
            InitLevel = InitLevel,
            ResistanceAtLastCheck = ResistanceAtLastCheck,
            LastUsedTime = LastUsedTime,
        };
    }

    public class PropertiesTextureMap
    {
        public byte PartIndex { get; set; }
        public uint OldTexture { get; set; }
        public uint NewTexture { get; set; }

        public PropertiesTextureMap Clone() => new PropertiesTextureMap
        {
            PartIndex = PartIndex,
            OldTexture = OldTexture,
            NewTexture = NewTexture,
        };
    }

    partial class PropertiesExtensions
    {
        public static int GetCount(this IList<PropertiesTextureMap> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return 0;
            rwLock.EnterReadLock();
            try { return value.Count; }
            finally { rwLock.ExitReadLock(); }
        }

        public static List<PropertiesTextureMap> Clone(this IList<PropertiesTextureMap> value, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return null;
            rwLock.EnterReadLock();
            try { return new List<PropertiesTextureMap>(value); }
            finally { rwLock.ExitReadLock(); }
        }

        public static void CopyTo(this IList<PropertiesTextureMap> value, ICollection<PropertiesTextureMap> destination, ReaderWriterLockSlim rwLock)
        {
            if (value == null) return;
            rwLock.EnterReadLock();
            try { foreach (var entry in value) destination.Add(entry); }
            finally { rwLock.ExitReadLock(); }
        }
    }

    #endregion

    #region Weenie

    /// <summary>
    /// Only populated collections and dictionaries are initialized.
    /// We do this to conserve memory in ACE.Server
    /// Be sure to check for null first.
    /// </summary>
    public interface IWeenie
    {
        uint WeenieClassId { get; set; }
        WeenieType WeenieType { get; set; }

        IDictionary<PropertyBool, bool> PropertiesBool { get; set; }
        IDictionary<PropertyDataId, uint> PropertiesDID { get; set; }
        IDictionary<PropertyFloat, double> PropertiesFloat { get; set; }
        IDictionary<PropertyInstanceId, uint> PropertiesIID { get; set; }
        IDictionary<PropertyInt, int> PropertiesInt { get; set; }
        IDictionary<PropertyInt64, long> PropertiesInt64 { get; set; }
        IDictionary<PropertyString, string> PropertiesString { get; set; }

        IDictionary<PositionType, PropertiesPosition> PropertiesPosition { get; set; }

        IDictionary<int, float /* probability */> PropertiesSpellBook { get; set; }

        IList<PropertiesAnimPart> PropertiesAnimPart { get; set; }
        IList<PropertiesPalette> PropertiesPalette { get; set; }
        IList<PropertiesTextureMap> PropertiesTextureMap { get; set; }

        // Properties for all world objects that typically aren't modified over the original weenie
        ICollection<PropertiesCreateList> PropertiesCreateList { get; set; }
        ICollection<PropertiesEmote> PropertiesEmote { get; set; }
        HashSet<int> PropertiesEventFilter { get; set; }
        IList<PropertiesGenerator> PropertiesGenerator { get; set; } // Using a list per this: https://github.com/ACEmulator/ACE/pull/2616, however, no order is guaranteed for db records

        // Properties for creatures
        IDictionary<PropertyAttribute, PropertiesAttribute> PropertiesAttribute { get; set; }
        IDictionary<PropertyAttribute2nd, PropertiesAttribute2nd> PropertiesAttribute2nd { get; set; }
        IDictionary<CombatBodyPart, PropertiesBodyPart> PropertiesBodyPart { get; set; }
        IDictionary<Skill, PropertiesSkill> PropertiesSkill { get; set; }

        // Properties for books
        PropertiesBook PropertiesBook { get; set; }
        IList<PropertiesBookPageData> PropertiesBookPageData { get; set; }
    }

    /// <summary>
    /// Only populated collections and dictionaries are initialized.
    /// We do this to conserve memory in ACE.Server
    /// Be sure to check for null first.
    /// </summary>
    public class Weenie : IWeenie
    {
        public uint WeenieClassId { get; set; }
        public string ClassName { get; set; }
        public WeenieType WeenieType { get; set; }

        public IDictionary<PropertyBool, bool> PropertiesBool { get; set; }
        public IDictionary<PropertyDataId, uint> PropertiesDID { get; set; }
        public IDictionary<PropertyFloat, double> PropertiesFloat { get; set; }
        public IDictionary<PropertyInstanceId, uint> PropertiesIID { get; set; }
        public IDictionary<PropertyInt, int> PropertiesInt { get; set; }
        public IDictionary<PropertyInt64, long> PropertiesInt64 { get; set; }
        public IDictionary<PropertyString, string> PropertiesString { get; set; }

        public IDictionary<PositionType, PropertiesPosition> PropertiesPosition { get; set; }

        public IDictionary<int, float /* probability */> PropertiesSpellBook { get; set; }

        public IList<PropertiesAnimPart> PropertiesAnimPart { get; set; }
        public IList<PropertiesPalette> PropertiesPalette { get; set; }
        public IList<PropertiesTextureMap> PropertiesTextureMap { get; set; }

        // Properties for all world objects that typically aren't modified over the original weenie
        public ICollection<PropertiesCreateList> PropertiesCreateList { get; set; }
        public ICollection<PropertiesEmote> PropertiesEmote { get; set; }
        public HashSet<int> PropertiesEventFilter { get; set; }
        public IList<PropertiesGenerator> PropertiesGenerator { get; set; }

        // Properties for creatures
        public IDictionary<PropertyAttribute, PropertiesAttribute> PropertiesAttribute { get; set; }
        public IDictionary<PropertyAttribute2nd, PropertiesAttribute2nd> PropertiesAttribute2nd { get; set; }
        public IDictionary<CombatBodyPart, PropertiesBodyPart> PropertiesBodyPart { get; set; }
        public IDictionary<Skill, PropertiesSkill> PropertiesSkill { get; set; }

        // Properties for books
        public PropertiesBook PropertiesBook { get; set; }
        public IList<PropertiesBookPageData> PropertiesBookPageData { get; set; }
    }

    public static partial class WeenieExtensions
    {
        // =====================================
        // Get
        // Bool, DID, Float, IID, Int, Int64, String, Position
        // =====================================
        public static bool? GetProperty(this Weenie weenie, PropertyBool property) => weenie.PropertiesBool == null || !weenie.PropertiesBool.TryGetValue(property, out var value) ? null : (bool?)value;
        public static uint? GetProperty(this Weenie weenie, PropertyDataId property) => weenie.PropertiesDID == null || !weenie.PropertiesDID.TryGetValue(property, out var value) ? null : (uint?)value;
        public static double? GetProperty(this Weenie weenie, PropertyFloat property) => weenie.PropertiesFloat == null || !weenie.PropertiesFloat.TryGetValue(property, out var value) ? null : (double?)value;
        public static uint? GetProperty(this Weenie weenie, PropertyInstanceId property) => weenie.PropertiesIID == null || !weenie.PropertiesIID.TryGetValue(property, out var value) ? null : (uint?)value;
        public static int? GetProperty(this Weenie weenie, PropertyInt property) => weenie.PropertiesInt == null || !weenie.PropertiesInt.TryGetValue(property, out var value) ? null : (int?)value;
        public static long? GetProperty(this Weenie weenie, PropertyInt64 property) => weenie.PropertiesInt64 == null || !weenie.PropertiesInt64.TryGetValue(property, out var value) ? null : (long?)value;
        public static string GetProperty(this Weenie weenie, PropertyString property) => weenie.PropertiesString == null || !weenie.PropertiesString.TryGetValue(property, out var value) ? null : value;
        public static PropertiesPosition GetProperty(this Weenie weenie, PositionType property) => weenie.PropertiesPosition == null || !weenie.PropertiesPosition.TryGetValue(property, out var value) ? null : value;
        public static XPosition GetPosition(this Weenie weenie, PositionType property) => weenie.PropertiesPosition == null || !weenie.PropertiesPosition.TryGetValue(property, out var value) ? null : new XPosition(value.ObjCellId, value.PositionX, value.PositionY, value.PositionZ, value.RotationX, value.RotationY, value.RotationZ, value.RotationW);


        // =====================================
        // Utility
        // =====================================

        public static string GetName(this Weenie weenie) => weenie.GetProperty(PropertyString.Name);

        public static string GetPluralName(this Weenie weenie) => weenie.GetProperty(PropertyString.PluralName) ?? Grammar.Pluralize(weenie.GetProperty(PropertyString.Name));

        public static ItemType GetItemType(this Weenie weenie) => (ItemType)(weenie.GetProperty(PropertyInt.ItemType) ?? 0);

        public static int? GetValue(this Weenie weenie) => weenie.GetProperty(PropertyInt.Value);

        public static bool IsStackable(this Weenie weenie) => weenie.WeenieType switch
        {
            WeenieType.Stackable or WeenieType.Ammunition or WeenieType.Coin or WeenieType.CraftTool or WeenieType.Food or WeenieType.Gem or WeenieType.Missile or WeenieType.SpellComponent => true,
            _ => false,
        };
        public static bool IsStuck(this Weenie weenie) => weenie.GetProperty(PropertyBool.Stuck) ?? false;

        public static bool RequiresBackpackSlotOrIsContainer(this Weenie weenie) => (weenie.GetProperty(PropertyBool.RequiresBackpackSlot) ?? false) || weenie.WeenieType == WeenieType.Container;

        public static bool IsVendorService(this Weenie weenie) => weenie.GetProperty(PropertyBool.VendorService) ?? false;

        public static int GetStackUnitEncumbrance(this Weenie weenie)
        {
            if (weenie.IsStackable())
            {
                var stackUnitEncumbrance = weenie.GetProperty(PropertyInt.StackUnitEncumbrance);
                if (stackUnitEncumbrance != null) return stackUnitEncumbrance.Value;
            }
            return weenie.GetProperty(PropertyInt.EncumbranceVal) ?? 0;
        }

        public static int GetMaxStackSize(this Weenie weenie)
        {
            if (weenie.IsStackable())
            {
                var maxStackSize = weenie.GetProperty(PropertyInt.MaxStackSize);
                if (maxStackSize != null) return maxStackSize.Value;
            }
            return 1;
        }
    }

    partial class WeenieExtensions
    {
        public static Biota ConvertToBiota(this Weenie weenie, uint id, bool instantiateEmptyCollections = false, bool referenceWeenieCollectionsForCommonProperties = false)
        {
            var result = new Biota
            {
                Id = id,
                WeenieClassId = weenie.WeenieClassId,
                WeenieType = weenie.WeenieType
            };
            if (weenie.PropertiesBool != null && (instantiateEmptyCollections || weenie.PropertiesBool.Count > 0)) result.PropertiesBool = new Dictionary<PropertyBool, bool>(weenie.PropertiesBool);
            if (weenie.PropertiesDID != null && (instantiateEmptyCollections || weenie.PropertiesDID.Count > 0)) result.PropertiesDID = new Dictionary<PropertyDataId, uint>(weenie.PropertiesDID);
            if (weenie.PropertiesFloat != null && (instantiateEmptyCollections || weenie.PropertiesFloat.Count > 0)) result.PropertiesFloat = new Dictionary<PropertyFloat, double>(weenie.PropertiesFloat);
            if (weenie.PropertiesIID != null && (instantiateEmptyCollections || weenie.PropertiesIID.Count > 0)) result.PropertiesIID = new Dictionary<PropertyInstanceId, uint>(weenie.PropertiesIID);
            if (weenie.PropertiesInt != null && (instantiateEmptyCollections || weenie.PropertiesInt.Count > 0)) result.PropertiesInt = new Dictionary<PropertyInt, int>(weenie.PropertiesInt);
            if (weenie.PropertiesInt64 != null && (instantiateEmptyCollections || weenie.PropertiesInt64.Count > 0)) result.PropertiesInt64 = new Dictionary<PropertyInt64, long>(weenie.PropertiesInt64);
            if (weenie.PropertiesString != null && (instantiateEmptyCollections || weenie.PropertiesString.Count > 0)) result.PropertiesString = new Dictionary<PropertyString, string>(weenie.PropertiesString);
            if (weenie.PropertiesPosition != null && (instantiateEmptyCollections || weenie.PropertiesPosition.Count > 0))
            {
                result.PropertiesPosition = new Dictionary<PositionType, PropertiesPosition>(weenie.PropertiesPosition.Count);
                foreach (var kvp in weenie.PropertiesPosition) result.PropertiesPosition.Add(kvp.Key, kvp.Value.Clone());
            }
            if (weenie.PropertiesSpellBook != null && (instantiateEmptyCollections || weenie.PropertiesSpellBook.Count > 0)) result.PropertiesSpellBook = new Dictionary<int, float>(weenie.PropertiesSpellBook);
            if (weenie.PropertiesAnimPart != null && (instantiateEmptyCollections || weenie.PropertiesAnimPart.Count > 0))
            {
                result.PropertiesAnimPart = new List<PropertiesAnimPart>(weenie.PropertiesAnimPart.Count);
                foreach (var record in weenie.PropertiesAnimPart) result.PropertiesAnimPart.Add(record.Clone());
            }
            if (weenie.PropertiesPalette != null && (instantiateEmptyCollections || weenie.PropertiesPalette.Count > 0))
            {
                result.PropertiesPalette = new Collection<PropertiesPalette>();
                foreach (var record in weenie.PropertiesPalette) result.PropertiesPalette.Add(record.Clone());
            }
            if (weenie.PropertiesTextureMap != null && (instantiateEmptyCollections || weenie.PropertiesTextureMap.Count > 0))
            {
                result.PropertiesTextureMap = new List<PropertiesTextureMap>(weenie.PropertiesTextureMap.Count);
                foreach (var record in weenie.PropertiesTextureMap) result.PropertiesTextureMap.Add(record.Clone());
            }

            // Properties for all world objects that typically aren't modified over the original weenie

            if (referenceWeenieCollectionsForCommonProperties)
            {
                result.PropertiesCreateList = weenie.PropertiesCreateList;
                result.PropertiesEmote = weenie.PropertiesEmote;
                result.PropertiesEventFilter = weenie.PropertiesEventFilter;
                result.PropertiesGenerator = weenie.PropertiesGenerator;
            }
            else
            {
                if (weenie.PropertiesCreateList != null && (instantiateEmptyCollections || weenie.PropertiesCreateList.Count > 0))
                {
                    result.PropertiesCreateList = new Collection<PropertiesCreateList>();
                    foreach (var record in weenie.PropertiesCreateList) result.PropertiesCreateList.Add(record.Clone());
                }

                if (weenie.PropertiesEmote != null && (instantiateEmptyCollections || weenie.PropertiesEmote.Count > 0))
                {
                    result.PropertiesEmote = new Collection<PropertiesEmote>();
                    foreach (var record in weenie.PropertiesEmote) result.PropertiesEmote.Add(record.Clone());
                }

                if (weenie.PropertiesEventFilter != null && (instantiateEmptyCollections || weenie.PropertiesEventFilter.Count > 0)) result.PropertiesEventFilter = new HashSet<int>(weenie.PropertiesEventFilter);
                if (weenie.PropertiesGenerator != null && (instantiateEmptyCollections || weenie.PropertiesGenerator.Count > 0))
                {
                    result.PropertiesGenerator = new List<PropertiesGenerator>(weenie.PropertiesGenerator.Count);
                    foreach (var record in weenie.PropertiesGenerator) result.PropertiesGenerator.Add(record.Clone());
                }
            }

            // Properties for creatures

            if (weenie.PropertiesAttribute != null && (instantiateEmptyCollections || weenie.PropertiesAttribute.Count > 0))
            {
                result.PropertiesAttribute = new Dictionary<PropertyAttribute, PropertiesAttribute>(weenie.PropertiesAttribute.Count);
                foreach (var kvp in weenie.PropertiesAttribute) result.PropertiesAttribute.Add(kvp.Key, kvp.Value.Clone());
            }

            if (weenie.PropertiesAttribute2nd != null && (instantiateEmptyCollections || weenie.PropertiesAttribute2nd.Count > 0))
            {
                result.PropertiesAttribute2nd = new Dictionary<PropertyAttribute2nd, PropertiesAttribute2nd>(weenie.PropertiesAttribute2nd.Count);
                foreach (var kvp in weenie.PropertiesAttribute2nd) result.PropertiesAttribute2nd.Add(kvp.Key, kvp.Value.Clone());
            }

            if (referenceWeenieCollectionsForCommonProperties) result.PropertiesBodyPart = weenie.PropertiesBodyPart;
            else
            {
                if (weenie.PropertiesBodyPart != null && (instantiateEmptyCollections || weenie.PropertiesBodyPart.Count > 0))
                {
                    result.PropertiesBodyPart = new Dictionary<CombatBodyPart, PropertiesBodyPart>(weenie.PropertiesBodyPart.Count);
                    foreach (var kvp in weenie.PropertiesBodyPart) result.PropertiesBodyPart.Add(kvp.Key, kvp.Value.Clone());
                }
            }

            if (weenie.PropertiesSkill != null && (instantiateEmptyCollections || weenie.PropertiesSkill.Count > 0))
            {
                result.PropertiesSkill = new Dictionary<Skill, PropertiesSkill>(weenie.PropertiesSkill.Count);
                foreach (var kvp in weenie.PropertiesSkill) result.PropertiesSkill.Add(kvp.Key, kvp.Value.Clone());
            }

            // Properties for books

            if (weenie.PropertiesBook != null) result.PropertiesBook = weenie.PropertiesBook.Clone();
            if (weenie.PropertiesBookPageData != null && (instantiateEmptyCollections || weenie.PropertiesBookPageData.Count > 0)) result.PropertiesBookPageData = new List<PropertiesBookPageData>(weenie.PropertiesBookPageData);

            return result;
        }
    }

    #endregion
}
