using System;
using System.Collections.Generic;
using System.Linq;

namespace Employee_Survey.Application
{
    public static class ScoringAllocator
    {
        // Trọng số theo độ khó
        private static readonly Dictionary<string, double> WeightByDiff =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Junior"] = 1.0,
                ["Middle"] = 1.2,
                ["Senior"] = 1.5
            };

        /// <summary>
        /// Phân bổ điểm cho từng câu. Tổng điểm == totalScore (sau chuẩn hoá).
        /// - Non-essay chia theo trọng số Difficulty (largest remainder 0.01).
        /// - Essay: nếu essayCount>0 & essayReserved<=0 => mặc định 2.0; chia đều cho essay.
        /// </summary>
        public static IReadOnlyList<(string questionId, decimal points)> Allocate(
            IEnumerable<(string Id, string Type, string? Difficulty)> picked,
            decimal totalScore, decimal essayReserved = 0m, int essayCount = 0)
        {
            var list = picked?.ToList() ?? new();
            if (list.Count == 0 || totalScore <= 0m)
                return Array.Empty<(string, decimal)>();

            var essays = list.Where(x => string.Equals(x.Type, "Essay", StringComparison.OrdinalIgnoreCase)).ToList();
            var autos = list.Where(x => !string.Equals(x.Type, "Essay", StringComparison.OrdinalIgnoreCase)).ToList();

            // Nếu có essay nhưng chưa set essayReserved -> set mặc định 2.0
            if (essayCount > 0 && essayReserved <= 0m)
                essayReserved = 2m;

            // Budget non-essay
            decimal autosBudget = Math.Max(0m, totalScore - essayReserved);
            if (autos.Count == 0) autosBudget = 0m;

            // 1) Trọng số Difficulty cho non-essay
            var weights = autos.Select(a =>
            {
                var diff = string.IsNullOrWhiteSpace(a.Difficulty) ? "Middle" : a.Difficulty!;
                var w = WeightByDiff.TryGetValue(diff, out var v) ? v : 1.0;
                return (a.Id, w);
            }).ToList();

            double sumW = weights.Sum(x => x.w);
            if (sumW <= 0.0) sumW = Math.Max(1, autos.Count);

            // 2) Điểm thô theo tỉ lệ
            var raw = weights.Select(x =>
            {
                var val = autosBudget * (decimal)(x.w / sumW);
                var flo = Math.Floor(val * 100m) / 100m; // floor 2 chữ số
                var rem = val - flo;
                return (x.Id, val, flo, rem);
            }).ToList();

            // 3) Largest remainder (0.01)
            var rounded = raw.ToDictionary(r => r.Id, r => r.flo);
            decimal assigned = rounded.Values.Sum();
            int remaining = (int)Math.Round((autosBudget - assigned) * 100m, MidpointRounding.AwayFromZero);
            if (remaining < 0) remaining = 0;

            foreach (var id in raw.OrderByDescending(x => x.rem).Select(x => x.Id))
            {
                if (remaining <= 0) break;
                rounded[id] += 0.01m;
                remaining--;
            }

            var result = new List<(string questionId, decimal points)>();
            result.AddRange(rounded.Select(kv => (kv.Key, kv.Value)));

            // 4) Essay
            if (essays.Any())
            {
                decimal essayBudget = essayReserved > 0m
                    ? essayReserved
                    : Math.Max(0m, totalScore - result.Sum(x => x.points)); // fallback hiếm

                if (essayBudget > 0m)
                {
                    var perEssay = Math.Round(essayBudget / essays.Count, 2, MidpointRounding.AwayFromZero);
                    foreach (var e in essays)
                        result.Add((e.Id, perEssay));
                }
                else
                {
                    foreach (var e in essays)
                        result.Add((e.Id, 0m));
                }
            }

            // 5) Chuẩn hoá tổng về đúng totalScore
            var diffSum = totalScore - result.Sum(x => x.points);
            if (result.Count > 0 && Math.Abs(diffSum) >= 0.01m)
            {
                var first = result[0];
                result[0] = (first.questionId, Math.Round(first.points + diffSum, 2, MidpointRounding.AwayFromZero));
            }

            // Không âm
            for (int i = 0; i < result.Count; i++)
                if (result[i].points < 0m) result[i] = (result[i].questionId, 0m);

            return result;
        }
    }
}
