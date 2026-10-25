import { MerchantRule } from '../../core/services/merchant-rules.service';
import { matchCategoryRule } from './rule-matching';
const rule:MerchantRule={id:1,matchType:'MerchantExact',pattern:'Netflix.com',categoryId:1,categoryName:'Subscriptions',categoryType:'Expense',categoryActive:true,isActive:true,editVersion:1};
const row={merchant:'NETFLIX.COM',description:'NETFLIX SUBSCRIPTION CHARGE',direction:'Out' as const,classification:'',excluded:false};
describe('Import category rules',()=>{
  it('prioritizes exact merchant matches over description phrases',()=>{expect(matchCategoryRule([{...rule,id:2,matchType:'DescriptionContains',pattern:'NETFLIX SUBSCRIPTION',categoryId:2},rule],row).rule?.id).toBe(1);});
  it('uses the longest description phrase and treats disagreeing ties as conflicts',()=>{
    const short={...rule,matchType:'DescriptionContains' as const,pattern:'NETFLIX'};const long={...short,id:2,pattern:'NETFLIX SUBSCRIPTION',categoryId:2};
    expect(matchCategoryRule([short,long],row).rule?.id).toBe(2);
    expect(matchCategoryRule([long,{...long,id:3,categoryId:3}],row)).toEqual({conflict:true});
    expect(matchCategoryRule([long,{...long,id:3}],row).rule?.id).toBe(2);
  });
  it('skips inactive rules, archived categories, wrong directions, transfers, and excluded rows',()=>{
    for(const r of [{...rule,isActive:false},{...rule,categoryActive:false},{...rule,categoryType:'Income' as const}])expect(matchCategoryRule([r],row).rule).toBeUndefined();
    for(const r of [{...row,excluded:true},{...row,classification:'Transfer'},{...row,categoryChoice:'transfer'}])expect(matchCategoryRule([rule],r).rule).toBeUndefined();
  });
  it('requires an actual merchant value for exact rules and matches punctuation and case consistently',()=>{
    expect(matchCategoryRule([rule],{...row,merchant:null}).rule).toBeUndefined();
    expect(matchCategoryRule([rule],{...row,merchant:' netflix.COM '}).rule?.id).toBe(1);
    expect(matchCategoryRule([rule],{...row,merchant:'Netflix.com extra'}).rule).toBeUndefined();
  });
});
