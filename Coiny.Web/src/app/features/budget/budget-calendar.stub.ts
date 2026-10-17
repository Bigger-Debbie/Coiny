// Shared test double; production code never imports this file.
import { Component, Input, output } from '@angular/core';
@Component({ selector: 'app-budget-calendar', template: '' })
export class BudgetCalendarStub { readonly sourceChange = output<string | null>(); readonly selectMonth = output<string>(); @Input() month: unknown; @Input() source: string | null = null; }
