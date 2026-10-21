import { TestBed } from '@angular/core/testing';
import { ColumnResize } from './column-resize';
describe('ColumnResize',()=>{
  function render(){const f=TestBed.createComponent(ColumnResize);f.componentRef.setInput('label','Description');f.componentRef.setInput('width',320);f.componentRef.setInput('min',160);f.componentRef.setInput('max',720);f.componentRef.setInput('defaultWidth',320);f.detectChanges();return f;}
  it('supports keyboard resizing with bounds and accessible size values',()=>{
    const f=render();const changed=vi.fn();f.componentInstance.resized.subscribe(changed);const button=f.nativeElement.querySelector('button');
    expect(button.getAttribute('role')).toBe('separator');expect(button.getAttribute('aria-valuenow')).toBe('320');
    button.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowRight',bubbles:true}));expect(changed).toHaveBeenLastCalledWith(336);
    button.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowLeft',shiftKey:true,bubbles:true}));expect(changed).toHaveBeenLastCalledWith(256);
    button.dispatchEvent(new KeyboardEvent('keydown',{key:'Home',bubbles:true}));expect(changed).toHaveBeenLastCalledWith(160);
    button.dispatchEvent(new KeyboardEvent('keydown',{key:'End',bubbles:true}));expect(changed).toHaveBeenLastCalledWith(720);
    button.dispatchEvent(new MouseEvent('dblclick'));expect(changed).toHaveBeenLastCalledWith(320);
  });
  it('captures pointer drags and clamps widths without responding after release',()=>{
    const f=render();const c=f.componentInstance;const changed=vi.fn();c.resized.subscribe(changed);
    const target={setPointerCapture:vi.fn(),hasPointerCapture:()=>true,releasePointerCapture:vi.fn()};
    const event=(x:number)=>({isPrimary:true,button:0,pointerId:4,clientX:x,currentTarget:target,preventDefault:vi.fn(),stopPropagation:vi.fn()} as unknown as PointerEvent);
    c.start(event(100));expect(target.setPointerCapture).toHaveBeenCalledWith(4);c.move(event(140));expect(changed).toHaveBeenLastCalledWith(360);
    c.move(event(-500));expect(changed).toHaveBeenLastCalledWith(160);c.move(event(2000));expect(changed).toHaveBeenLastCalledWith(720);
    c.finish(event(2000));expect(target.releasePointerCapture).toHaveBeenCalledWith(4);changed.mockClear();c.move(event(300));expect(changed).not.toHaveBeenCalled();
  });
});
