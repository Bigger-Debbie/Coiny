import { TransactionImportRequest, TransactionImportResult, TransactionImportValidation } from './transaction-import';
export interface SessionReference { accountId:number; rowId:number|null; transactionId:number|null; expectedEditVersion:number|null; }
export interface SessionEntry { reference:SessionReference; accountName:string; date:string; amount:number; direction:'In'|'Out'; description:string; }
export interface SessionLink { first:SessionReference; second:SessionReference; }
export interface SessionSuggestion { first:SessionEntry; second:SessionEntry; }
export interface ImportSessionRequest { submissionId:string; files:TransactionImportRequest[]; links:SessionLink[]; }
export interface ImportSessionValidation { canImport:boolean; files:{accountId:number;validation:TransactionImportValidation}[]; suggestions:SessionSuggestion[]; moreSuggestions:boolean; }
export interface ImportSessionResult { submissionId:string; files:TransactionImportResult[]; linkedPairs:number; }
