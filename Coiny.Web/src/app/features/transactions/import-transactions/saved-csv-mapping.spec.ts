import { CsvMappingSettings } from '../../../core/services/csv-mappings.service';
import { rebindCsvMapping } from './saved-csv-mapping';
const settings:CsvMappingSettings={headers:['Date','Description','Debit','Credit','Status'],mapping:{dateColumn:0,descriptionColumn:1,dateFormat:'MM/DD/YY',amount:{mode:'split',debitColumn:2,creditColumn:3},statusColumn:4,excludedStatuses:['Pending']},encoding:'utf-8',delimiter:',',headerLine:1,balanceOrder:null};
describe('Saved CSV header matching',()=>{
 it('remaps moved columns by their trimmed case-insensitive names and ignores new unmapped columns',()=>{
  const m=rebindCsvMapping(settings,['Credit','Status','Date','Extra','Debit','Description']);expect(m.dateColumn).toBe(2);expect(m.descriptionColumn).toBe(5);expect(m.amount).toEqual({mode:'split',debitColumn:4,creditColumn:0});expect(m.statusColumn).toBe(1);expect(settings.mapping.dateColumn).toBe(0);
 });
 it('rejects missing and ambiguous mapped headers rather than reusing stale indexes',()=>{
  expect(()=>rebindCsvMapping(settings,['Date','Description','Amount','Status'])).toThrow(/missing or ambiguous/);
  expect(()=>rebindCsvMapping(settings,['Date','Description','Debit','Credit','Status','Date'])).toThrow(/ambiguous/);
  const duplicates={...settings,headers:['Date','Description','Amount','Amount','Status']};expect(rebindCsvMapping(duplicates,duplicates.headers).amount).toEqual({mode:'split',debitColumn:2,creditColumn:3});expect(()=>rebindCsvMapping(duplicates,['Description','Date','Amount','Amount','Status'])).toThrow(/ambiguous/);
 });
});
