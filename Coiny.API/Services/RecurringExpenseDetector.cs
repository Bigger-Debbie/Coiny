using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Coiny.API.DTOs.Recurring;
namespace Coiny.API.Services;

public record RecurringInput(int Id, string Merchant, DateOnly Date, decimal Amount, string AccountName, string Description);
public static class RecurringExpenseDetector
{
    // Keep digits and full merchant identities: merging similar-looking merchants can hide real bills.
    public static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC).ToUpperInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    private static decimal Median(IEnumerable<decimal> values) { var a=values.Order().ToArray(); return (a[(a.Length-1)/2]+a[a.Length/2])/2; }
    public static IReadOnlyList<RecurringSuggestion> Detect(IEnumerable<RecurringInput> inputs, DateOnly today)
    {
        var results=new List<RecurringSuggestion>();
        foreach (var group in inputs.Where(x=>x.Amount>0 && x.Date<=today).GroupBy(x=>Normalize(x.Merchant)))
        {
            if (group.Key.Length<3) continue;
            var rows=group.OrderBy(x=>x.Date).ThenBy(x=>x.Id).ToArray();
            if (rows.Length<3 || rows.GroupBy(x=>x.Date).Any(g=>g.Count()>1)) continue;
            var gaps=rows.Zip(rows.Skip(1),(a,b)=>b.Date.DayNumber-a.Date.DayNumber).ToArray();
            var typical=Median(rows.Select(x=>x.Amount));
            // Permit variable utilities while rejecting unrelated spending with a wide amount range.
            if (rows.Any(r=>Math.Abs(r.Amount-typical)>Math.Max(5,typical*.75m))) continue;
            var candidates=new[] { ("Weekly",7,2),("EveryTwoWeeks",14,3),("Monthly",30,5),("EverySixMonths",182,14),("Yearly",365,21) };
            var cadence=candidates.FirstOrDefault(c=>gaps.All(g=>Math.Abs(g-c.Item2)<=c.Item3));
            if (cadence.Item1 is null || today.DayNumber-rows[^1].Date.DayNumber>cadence.Item2*2+cadence.Item3) continue;
            var last=rows[^1].Date;
            var next=cadence.Item1 switch { "Monthly"=>last.AddMonths(1),"EverySixMonths"=>last.AddMonths(6),"Yearly"=>last.AddYears(1),_=>last.AddDays(cadence.Item2) };
            var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key+"|"+cadence.Item1)));
            results.Add(new(key,rows[^1].Merchant.Trim(),cadence.Item1,decimal.Round(typical,2),rows.Min(r=>r.Amount),rows.Max(r=>r.Amount),last,next,rows.Length,"NeedsReview",null,null,
                rows.TakeLast(12).Reverse().Select(r=>new RecurringEvidence(r.Id,r.Date,r.Amount,r.AccountName,r.Description)).ToArray()));
        }
        return results.OrderBy(r=>r.NextExpected).ThenBy(r=>r.Merchant).ToArray();
    }
}
