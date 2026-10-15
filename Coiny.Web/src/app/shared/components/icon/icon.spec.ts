import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Icon } from './icon';

describe('Icon', () => {
  let component: Icon;
  let fixture: ComponentFixture<Icon>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Icon],
    }).compileComponents();

    fixture = TestBed.createComponent(Icon);
    fixture.componentRef.setInput('name', 'accounts');
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('supports the up and down asset shapes', () => {
    for (const name of ['up', 'down']) {
      fixture.componentRef.setInput('name', name);
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('svg').getAttribute('viewBox')).toBe('0 0 25.93 25.92');
      expect(fixture.nativeElement.querySelector('path').getAttribute('d')).toBeTruthy();
    }
  });
});
