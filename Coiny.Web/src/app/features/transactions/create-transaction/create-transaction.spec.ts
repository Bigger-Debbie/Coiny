import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CreateTransaction } from './create-transaction';

describe('CreateTransaction', () => {
  let component: CreateTransaction;
  let fixture: ComponentFixture<CreateTransaction>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateTransaction],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateTransaction);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
