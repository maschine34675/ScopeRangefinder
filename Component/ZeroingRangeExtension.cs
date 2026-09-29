using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using JsonType;

namespace ScopeRangefinder
{
    internal static class ZeroingRangeExtension
    {
        private const string LauncherReflexSightId = "6284bd5f95250a29bc628a30";
        private static readonly Dictionary<SightModTemplate, int[][]> Originals =
            new Dictionary<SightModTemplate, int[][]>();

        private static int _maxDistance;
        private static ItemTemplates _appliedTo;
        internal static void Apply(int maxDistance)
        {
            if (maxDistance != _maxDistance)
            {
                _maxDistance = maxDistance;
                _appliedTo = null;
                foreach (KeyValuePair<SightModTemplate, int[][]> entry in new List<KeyValuePair<SightModTemplate, int[][]>>(Originals))
                {
                    entry.Key.CalibrationDistances = Build(entry.Key, entry.Value, maxDistance);
                    if (maxDistance <= 0)
                    {
                        Originals.Remove(entry.Key);
                    }
                }
            }

            if (maxDistance <= 0 || !Singleton<ItemFactory>.Instantiated)
            {
                return;
            }

            ItemTemplates registry = Singleton<ItemFactory>.Instance.ItemTemplates;
            if (registry == null || ReferenceEquals(registry, _appliedTo))
            {
                return;
            }
            _appliedTo = registry;
            var current = new Dictionary<SightModTemplate, int[][]>();
            int extendedSights = 0;
            foreach (ItemTemplate template in registry.Values)
            {
                if (template is SightModTemplate sight && !current.ContainsKey(sight))
                {
                    int[][] original = Originals.TryGetValue(sight, out int[][] recorded)
                        ? recorded
                        : sight.CalibrationDistances;
                    current.Add(sight, original);
                    sight.CalibrationDistances = Build(sight, original, maxDistance);
                    if (!ReferenceEquals(sight.CalibrationDistances, original))
                    {
                        extendedSights++;
                    }
                }
            }

            if (Plugin.LogScopeKeys.Value)
            {
                Plugin.LogSource?.LogInfo(
                    $"Zeroing steps extended to {maxDistance} m on {extendedSights} of {current.Count} sight templates.");
            }

            Originals.Clear();
            foreach (KeyValuePair<SightModTemplate, int[][]> entry in current)
            {
                Originals.Add(entry.Key, entry.Value);
            }
        }
        internal static bool HasStaleCalibration(IEnumerable<SightComponent> sights)
        {
            foreach (SightComponent sight in sights)
            {
                if (HasCalibrationOfOtherLength(sight))
                {
                    return true;
                }
            }

            return false;
        }
        private static bool HasCalibrationOfOtherLength(SightComponent sight)
        {
            Vector3Lists points = new Vector3Lists(sight.OpticCalibrationPoints);
            for (int scope = 0; scope < sight.ScopesCount; scope++)
            {
                int[] steps = sight.GetScopeCalibrationDistances(scope);
                int built = points.LengthOf(scope);
                if (steps != null && built > 0 && built != steps.Length)
                {
                    return true;
                }
            }

            return false;
        }

        private static int[][] Build(SightModTemplate template, int[][] original, int maxDistance)
        {
            if (original == null || maxDistance <= 0 || template._id.ToString() == LauncherReflexSightId)
            {
                return original;
            }

            int[][] result = null;
            for (int mode = 0; mode < original.Length; mode++)
            {
                int[] extended = Extend(original[mode], maxDistance);
                if (!ReferenceEquals(extended, original[mode]))
                {
                    result ??= (int[][])original.Clone();
                    result[mode] = extended;
                }
            }

            return result ?? original;
        }
        private static int[] Extend(int[] steps, int maxDistance)
        {
            if (steps == null || steps.Length < 2)
            {
                return steps;
            }

            int last = int.MinValue;
            int belowLast = int.MinValue;
            foreach (int step in steps)
            {
                if (step > last)
                {
                    belowLast = last;
                    last = step;
                }
                else if (step < last && step > belowLast)
                {
                    belowLast = step;
                }
            }

            int stepSize = belowLast == int.MinValue ? 0 : last - belowLast;
            if (stepSize <= 0 || last + stepSize > maxDistance)
            {
                return steps;
            }

            var extended = new List<int>(steps);
            for (int distance = last + stepSize; distance <= maxDistance; distance += stepSize)
            {
                extended.Add(distance);
            }

            return extended.ToArray();
        }
        private readonly struct Vector3Lists
        {
            private readonly UnityEngine.Vector3[][] _lists;

            internal Vector3Lists(UnityEngine.Vector3[][] lists)
            {
                _lists = lists;
            }

            internal int LengthOf(int scope)
            {
                return _lists != null && scope < _lists.Length && _lists[scope] != null
                    ? _lists[scope].Length
                    : 0;
            }
        }
    }
}
