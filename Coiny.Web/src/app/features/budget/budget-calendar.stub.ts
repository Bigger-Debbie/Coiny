// Shared test double; production code never imports this file.
import { Component, Input, output } from '@angular/core';
@Component({ selector: 'app-budget-calendar', template: '' })
export class BudgetCalendarStub { readonly sourceChange = output<string | null>(); readonly selectMonth = output<string>(); readonly paymentChanged = output<void>(); @Input() month: unknown; @Input() source: string | null = null; @Input() controlledPeriod: unknown; }

@Component({ selector: 'app-budget-actuals', template: '' })
export class BudgetActualsStub { readonly refreshed = output<void>(); @Input() month = ''; @Input() refreshKey = 0; @Input() periodAsOf: string | null = null; }
