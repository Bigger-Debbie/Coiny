import { CsvMappingSettings } from '../../../core/services/csv-mappings.service';
import { CsvMapping } from './csv-mapping';
import { CsvError } from './csv-parser';
const header=(value:string)=>value.trim().replace(/\s+/g,' ').toLowerCase();
export function rebindCsvMapping(settings:CsvMappingSettings,currentHeaders:readonly string[]):CsvMapping {
 const saved=settings.headers.map(header);const current=currentHeaders.map(header);
 const identical=saved.length===current.length && saved.every((h,i)=>h===current[i]);
 const resolve=(index:number|undefined|null):number|undefined=>{
  if(index==null)return undefined;
  const name=saved[index];if(!name)throw new CsvError('The saved mapping has a blank or invalid mapped header. Match the columns manually.');
  const matches=current.map((h,i)=>h===name?i:-1).filter(i=>i>=0);
  if(matches.length!==1 || saved.filter(h=>h===name).length!==1){
   if(identical)return index;
   throw new CsvError(`The saved column “${settings.headers[index]}” is missing or ambiguous. Match the columns manually or choose another mapping.`);
  }
  return matches[0];
 };
 const m=settings.mapping;const a=m.amount;
 return {...m,dateColumn:resolve(m.dateColumn)!,descriptionColumn:resolve(m.descriptionColumn)!,
  amount:a.mode==='split'?{mode:'split',debitColumn:resolve(a.debitColumn)!,creditColumn:resolve(a.creditColumn)!}:a.mode==='signed'?{mode:'signed',column:resolve(a.column)!,positiveMeans:a.positiveMeans}:{mode:'indicator',column:resolve(a.column)!,directionColumn:resolve(a.directionColumn)!,directions:{...a.directions}},
  excludedStatuses:[...(m.excludedStatuses??[])],statusColumn:resolve(m.statusColumn),classificationColumn:resolve(m.classificationColumn),balanceColumn:resolve(m.balanceColumn),merchantColumn:resolve(m.merchantColumn),accountColumn:resolve(m.accountColumn)};
}
