from pathlib import Path
import re,json,io,zipfile,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parent
W,H,S=1157,1359,3
INK=(8,7,6)
def points(path):
 t=re.findall(r'[MLCQZ]|-?\d+(?:\.\d+)?',path);i=0;out=[];pos=np.zeros(2);start=pos
 while i<len(t):
  c=t[i];i+=1
  if c=='Z':out.append(tuple(start));continue
  k={'M':2,'L':2,'Q':4,'C':6}[c];v=np.array(list(map(float,t[i:i+k]))).reshape(-1,2);i+=k
  if c in ('M','L'):
   pos=v[0];out.append(tuple(pos))
   if c=='M':start=pos.copy()
  else:
   for u in np.linspace(0,1,100):
    q=(1-u)**3*pos+3*(1-u)**2*u*v[0]+3*(1-u)*u*u*v[1]+u**3*v[2] if c=='C' else (1-u)**2*pos+2*(1-u)*u*v[0]+u*u*v[1]
    out.append(tuple(q))
   pos=v[-1]
 return [(round(x*S),round(y*S)) for x,y in out]
yy,xx=np.mgrid[:H*S,:W*S]
def gradient(c0,c1):
 u=np.clip(.65*xx/(W*S)+.35*yy/(H*S),0,1)
 a=np.zeros((H*S,W*S,4),np.uint8)
 for k in range(3):a[:,:,k]=np.rint(c0[k]*(1-u)+c1[k]*u)
 a[:,:,3]=255
 return Image.fromarray(a)
fills={'hair':gradient((91,76,62),(73,65,57)), 'skin':gradient((255,253,247),(245,237,229)), 'mint':gradient((191,230,217),(152,214,199)), 'shorts':gradient((104,119,121),(81,96,100)), 'purple':gradient((83,76,122),(53,51,81)), 'highlight':gradient((255,222,172),(255,215,161)), 'neck':gradient((250,233,221),(237,203,195))}
# Fit smooth paint fields to the reference palette, rather than copying cutouts.
# Every connected part samples the same field in the same original coordinates.
reference=np.array(Image.open(P.parent/'player_side_20260922/player_side_assembled.png').convert('RGBA'))
ry,rx=np.mgrid[:H,:W];r,g,b=reference[:,:,:3].transpose(2,0,1).astype(float)
paint_masks={
 'hair':(ry<790)&(r>48)&(r<115)&(r>g+4)&(g>b+2),
 'skin':(r>228)&(g>219)&(b>205)&(r>g)&(ry>360),
 'mint':(ry>665)&(ry<1060)&(g>165)&(g>r+15)&(b>r+8)&(b<g+5),
 'shorts':(ry>1000)&(ry<1135)&(r>65)&(r<140)&(g>r+3)&(b>=g-3)}
sx=xx/(W*S);sy=yy/(H*S)
for material,mask in paint_masks.items():
 mask &= reference[:,:,3]>240
 iy,ix=np.where(mask);iy=iy[::8];ix=ix[::8];tx=ix/W;ty=iy/H
 X=np.column_stack([np.ones(len(ix)),tx,ty,tx*tx,tx*ty,ty*ty]);Y=reference[iy,ix,:3].astype(float)
 for repeat in range(3):
  co=np.linalg.lstsq(X,Y,rcond=None)[0];err=np.linalg.norm(X@co-Y,axis=1);good=err<np.percentile(err,85);X=X[good];Y=Y[good]
 imagearr=np.zeros((H*S,W*S,4),np.uint8)
 for ch in range(3):
  z=co[0,ch]+co[1,ch]*sx+co[2,ch]*sy+co[3,ch]*sx*sx+co[4,ch]*sx*sy+co[5,ch]*sy*sy
  imagearr[:,:,ch]=np.clip(z,Y[:,ch].min()-8,Y[:,ch].max()+8).clip(0,255).astype(np.uint8)
 imagearr[:,:,3]=255;fills[material]=Image.fromarray(imagearr)
defs={}; layers={}
def layer(n):
 global current,name
 name=n;current=Image.new('RGBA',(W*S,H*S));defs[n]=[]
def shape(path,fill,stroke=None,width=7):
 pts=points(path);mask=Image.new('L',current.size);ImageDraw.Draw(mask).polygon(pts,fill=255)
 col=fills[fill] if isinstance(fill,str) else Image.new('RGBA',current.size,fill)
 current.paste(col,(0,0),mask)
 if stroke:line(path,width)
 defs[name].append({'path':path,'fill':fill,'stroke':bool(stroke),'width':width})
def line(path,width=7):
 pts=points(path); brush=ImageDraw.Draw(current); thickness=round(width*S*1.45)
 brush.line(pts,fill=INK,width=thickness,joint='curve')
 radius=thickness/2
 for x,y in (pts[0],pts[-1]):brush.ellipse((x-radius,y-radius,x+radius,y+radius),fill=INK)
 defs[name].append({'path':path,'strokeOnly':True,'width':width})
def done():layers[name]=current.resize((W,H),Image.Resampling.LANCZOS)

layer('backHair')
shape('M 254 441 C 244 297 335 150 491 130 C 599 106 724 158 760 281 C 786 410 678 610 604 692 L 498 770 L 439 783 L 404 780 L 399 762 L 390 780 L 340 777 L 330 760 L 320 774 L 307 770 L 292 733 L 273 750 L 275 729 L 260 743 C 270 634 254 537 254 441 Z','hair')
line('M 254 441 C 254 537 270 634 260 743 L 275 729 L 273 750 L 292 733 L 307 770 L 320 774 L 330 760 L 340 777 L 390 780 L 399 762 L 404 780 L 439 783',7)
done()

layer('lowerbody')
shape('M 362 1098 C 400 1034 452 1014 514 1019 C 606 1025 716 992 782 1004 C 836 1007 868 1038 879 1084 L 802 1149 L 588 1176 L 354 1131 Z','shorts')
line('M 514 1019 C 606 1025 716 992 782 1004 C 798 1005 808 1014 814 1025',8)
shape('M 814 1025 C 850 1035 878 1076 880 1115 C 881 1174 840 1211 775 1226 L 632 1187 L 634 1146 C 699 1110 771 1092 814 1025 Z','skin',True,8)
shape('M 778 1009 Q 794 1010 808 1021 C 798 1045 782 1065 759 1078 C 769 1055 776 1032 778 1009 Z',(131,148,149,255),True,6)
shape('M 398 1067 C 384 1086 368 1100 363 1124 C 345 1182 381 1229 444 1241 C 543 1260 674 1265 756 1244 C 784 1236 793 1218 778 1198 C 747 1157 679 1134 591 1107 C 523 1085 462 1064 398 1067 Z','skin',True,8)
done()

layer('body')
shape('M 512 661 C 550 676 593 715 638 737 C 654 802 663 893 683 980 L 716 1080 C 638 1071 568 1022 499 1037 C 449 1046 406 1061 373 1068 Q 362 1070 366 1059 C 384 1010 397 959 406 907 C 424 803 455 706 512 661 Z','mint')
line('M 512 661 C 455 706 424 803 406 907 C 397 959 384 1010 366 1059 Q 362 1070 373 1068 C 406 1061 449 1046 499 1037 C 568 1022 638 1071 716 1080',7)
line('M 638 737 C 654 802 663 893 683 980 L 716 1080',7)
done()

layer('frontHair_2')
shape('M 494 358 C 507 345 531 362 544 403 C 563 503 547 614 510 667 C 524 562 483 477 494 358 Z','hair')
line('M 510 667 C 547 614 563 503 544 403',6)
done()

layer('arm2')
shape('M 634 840 L 688 853 C 713 918 755 1006 757 1062 C 758 1078 755 1097 745 1104 C 741 1115 730 1115 728 1101 C 728 1119 715 1120 713 1106 C 710 1118 699 1113 698 1101 C 673 1063 654 1003 646 951 Z','skin',True,7)
shape('M 605 714 C 648 728 683 804 705 857 C 691 871 670 880 650 885 C 640 817 625 768 605 714 Z','mint')
line('M 651 885 C 670 880 691 871 705 857 C 684 809 660 765 638 737',7)
done()

layer('arm1')
shape('M 481 873 L 595 857 C 617 925 655 1005 688 1052 C 704 1076 706 1100 696 1109 C 690 1115 684 1110 682 1100 C 687 1118 675 1125 668 1114 C 669 1129 655 1130 649 1118 C 643 1127 632 1121 625 1112 C 565 1070 521 997 498 932 Z','skin',True,7)
shape('M 513 663 C 546 679 574 709 587 750 L 628 885 C 578 909 512 932 449 922 C 442 882 440 844 449 803 C 460 744 483 690 513 663 Z','mint')
line('M 449 803 C 440 844 442 882 449 922 C 512 932 578 909 628 885 L 586 755',7)
done()

layer('head')
shape('M 494 426 C 469 352 506 259 563 227 C 660 169 786 219 824 300 C 845 345 846 404 845 460 L 845 494 C 842 516 847 525 862 531 C 881 538 859 557 852 568 C 855 595 835 638 810 653 C 770 678 694 686 635 683 L 634 740 C 583 722 539 690 507 668 L 515 605 C 468 620 430 596 422 562 C 404 517 421 461 455 438 C 469 429 481 425 494 426 Z','skin')
line('M 532 434 C 518 427 505 425 494 426 C 481 425 469 429 455 438 C 421 461 404 517 422 562 C 430 596 468 620 515 605',8)
line('M 824 300 C 845 345 846 404 845 460 L 845 494 C 842 516 847 525 862 531 C 881 538 859 557 852 568 C 855 595 835 638 810 653 C 770 678 694 686 635 683',8)
line('M 507 668 L 515 605',7)
shape('M 508 668 C 544 682 590 714 634 740 C 623 707 634 693 635 683 C 585 685 550 679 508 668 Z','neck')
line('M 508 668 C 544 682 590 714 634 740 L 643 748',7)
line('M 635 683 L 634 740',7)
line('M 487 514 C 505 512 520 515 536 521',8)
shape('M 663 449 L 806 470 C 804 515 789 550 763 560 C 732 574 702 556 685 526 C 671 502 669 476 663 449 Z','skin',True,8)
shape('M 712 457 L 804 470 C 797 507 785 537 763 537 C 734 536 716 504 712 457 Z','purple',True,7)
line('M 662 448 L 807 470',9)
shape('M 735 386 C 748 381 774 384 793 393 C 813 410 785 418 764 413 C 745 411 726 398 735 386 Z','hair',True,7)
line('M 782 633 C 790 622 799 621 806 632',7)
done()

layer('frontHair')
shape('M 254 440 C 250 296 324 174 447 130 C 575 85 724 122 798 201 C 843 250 872 306 881 366 C 823 364 778 369 727 378 L 708 278 L 688 385 L 628 411 L 573 432 C 521 431 489 407 447 437 C 394 445 332 459 254 477 Z','hair')
line('M 254 440 C 250 296 324 174 447 130 C 575 85 724 122 798 201 C 843 250 872 306 881 366 C 823 364 778 369 727 378 L 708 278 L 688 385 L 628 411',8)
line('M 254 440 L 254 478',7)
shape('M 499 260 C 530 227 575 201 622 188 C 636 185 642 205 651 217 C 684 201 714 188 754 204 C 717 215 685 229 652 244 C 637 251 629 233 624 219 C 585 228 551 257 525 279 C 513 282 487 274 499 260 Z','highlight')
done()

layer('frontHair_1')
shape('M 564 313 C 544 361 531 429 536 503 C 541 594 570 674 636 706 C 620 625 622 518 630 426 C 633 380 637 342 642 318 L 622 290 L 585 290 Z','hair')
line('M 564 313 C 544 361 531 429 536 503 C 541 594 570 674 636 706 C 620 625 622 518 630 426 C 633 380 637 342 642 318',8)
done()

order=list(layers)
assembled=Image.new('RGBA',(W,H))
for n in order:assembled.alpha_composite(layers[n])
assembled.save(P/'assembled.png')
(P/'parts').mkdir(exist_ok=True)
atlas=Image.new('RGBA',(2048,2048));x=y=24;rh=0;meta={'canvas':[W,H],'drawOrder':order,'layers':{},'shoulders':{'arm1':[513,686],'arm2':[620,735]}}
for n,im in layers.items():
 b=im.getbbox();cut=im.crop(b)
 if x+cut.width+24>2048:x=24;y+=rh+50;rh=0
 assert y+cut.height+24<2048
 atlas.paste(cut,(x,y));cut.save(P/'parts'/f'{n}.png');meta['layers'][n]={'rect':[x,y,cut.width,cut.height],'position':list(b[:2])}
 x+=cut.width+24;rh=max(rh,cut.height)
atlas.save(P/'atlas.png');(P/'layout.json').write_text(json.dumps(meta,indent=2));(P/'drawing_paths.json').write_text(json.dumps(defs,indent=2))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23)
bg=(220,229,232,255)
preview=Image.new('RGBA',atlas.size,bg);preview.alpha_composite(atlas);d=ImageDraw.Draw(preview)
for n,v in meta['layers'].items():
 x,y,w,h=v['rect'];d.text((x,y+h+3),n,font=font,fill='black')
preview.save(P/'atlas_preview.png')
ref=Image.open(P.parent/'player_side_20260922/player_side_assembled.png').convert('RGBA')
cmp=Image.new('RGBA',(W*2,H),bg);cmp.alpha_composite(ref);cmp.alpha_composite(assembled,(W,0));cmp.resize((1157,680),Image.Resampling.LANCZOS).save(P/'comparison.png')
v=Image.new('RGBA',(W,H),bg);v.alpha_composite(assembled);v.resize((579,680),Image.Resampling.LANCZOS).save(P/'assembled_preview.png')
print('Saved',P)
