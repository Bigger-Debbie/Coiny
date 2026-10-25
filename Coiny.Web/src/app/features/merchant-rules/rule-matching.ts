import { MerchantRule } from '../../core/services/merchant-rules.service';
export const normalizeRuleText=(value:string)=>value.normalize('NFKC').toUpperCase().replace(/[^\p{L}\p{N}]+/gu,' ').trim();
export function matchCategoryRule(rules:readonly MerchantRule[],row:{merchant:string|null;description:string;direction:'In'|'Out'|null;classification:string;excluded:boolean;categoryChoice?:string|null}):{rule?:MerchantRule;conflict:boolean} {
  if(row.excluded || !row.direction || row.categoryChoice==='transfer' || normalizeRuleText(row.classification)==='TRANSFER')return {conflict:false};
  const merchant=normalizeRuleText(row.merchant??'');const description=normalizeRuleText(row.description);
  const matches=rules.filter(r=>r.isActive && r.categoryActive && r.categoryType===(row.direction==='In'?'Income':'Expense') &&
    (r.matchType==='MerchantExact'?merchant===normalizeRuleText(r.pattern):description.includes(normalizeRuleText(r.pattern))));
  const exact=matches.filter(r=>r.matchType==='MerchantExact');
  const ranked=exact.length?exact:matches.filter(r=>normalizeRuleText(r.pattern).length===Math.max(0,...matches.map(m=>normalizeRuleText(m.pattern).length)));
  if(new Set(ranked.map(r=>r.categoryId)).size>1)return {conflict:true};
  return {rule:ranked.sort((a,b)=>a.id-b.id)[0],conflict:false};
}
