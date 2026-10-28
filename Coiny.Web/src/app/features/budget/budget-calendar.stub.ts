// Shared test double; production code never imports this file.
import { Component, Input, output } from '@angular/core';
@Component({ selector: 'app-budget-calendar', template: '' })
export class BudgetCalendarStub { readonly sourceChange = output<string | null>(); readonly selectMonth = output<string>(); readonly paymentChanged = output<void>(); @Input() month: unknown; @Input() source: string | null = null; @Input() controlledPeriod: unknown; }

@Component({ selector: 'app-budget-actuals', template: '' })
export class BudgetActualsStub { readonly refreshed = output<void>(); @Input() month = ''; @Input() refreshKey = 0; @Input() periodAsOf: string | null = null; }

@Component({selector:'app-savings-funds',template:''})
export class SavingsFundsStub { openFund(_key:string){return false;} edit(){}  @Input() accountId: number|null=null; @Input() draft: unknown; @Input() refreshKey=0; readonly changed=output<void>(); }
