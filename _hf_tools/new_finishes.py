#!/usr/bin/env python3
# 新規ローカル配色（顔据え置きマスク）。再開可能・時間制限つき（45秒sandbox対策）。
import os, sys, time
import numpy as np
from PIL import Image
HERE=os.path.dirname(os.path.abspath(__file__))
ASSETS=os.path.join(os.path.dirname(HERE),'assets')
OUT=os.path.join(ASSETS,'hero_skins')
ALL_JOBS=['warrior','wizard','gambler','ren','samurai','dragoon','paladin','sorcerer',
          'darkknight','fallen','reddragoon','gilgamesh','lucifer','odin','loki','valkyrie','magicblade']
NEW={
 'magma':(168,1.55,1.05),
 'fuchsia':(95,1.30,1.02),
 'lime':(285,1.30,1.04),
}
def recolor(im,hue,sm,bm):
    rgba=np.asarray(im.convert('RGBA'),dtype=np.uint8); a=rgba[...,3]
    hsv=np.asarray(Image.fromarray(rgba[...,:3],'RGB').convert('HSV'),dtype=np.float32)
    H,S,V=hsv[...,0],hsv[...,1],hsv[...,2]
    skin=(H>=5)&(H<=33)&(S>=38)&(S<=199)&(V>=76); tgt=(a>0)&(~skin)
    H[tgt]=(H+hue/360*255)[tgt]%256; S[tgt]=np.clip(S*sm,0,255)[tgt]; V[tgt]=np.clip(V*bm,0,255)[tgt]
    rgb=np.asarray(Image.fromarray(np.stack([H,S,V],-1).astype(np.uint8),'HSV').convert('RGB'))
    return Image.fromarray(np.dstack([rgb,a]),'RGBA')
def main():
    budget=float(sys.argv[1]) if sys.argv[1:] else 38.0
    t0=time.time()
    files=[f for f in sorted(os.listdir(ASSETS))
           if f.endswith('.png') and any(f.startswith('t_d_%s_'%j) for j in ALL_JOBS)]
    done=0; remaining=0
    for fid,(h,s,b) in NEW.items():
        d=os.path.join(OUT,fid); os.makedirs(d,exist_ok=True)
        for fn in files:
            outp=os.path.join(d,fn)
            if os.path.exists(outp): continue
            if time.time()-t0>budget: remaining+=1; continue
            recolor(Image.open(os.path.join(ASSETS,fn)),h,s,b).save(outp,compress_level=1); done+=1
    # report totals
    tot=len(files)*len(NEW)
    have=sum(len(os.listdir(os.path.join(OUT,fid))) for fid in NEW if os.path.isdir(os.path.join(OUT,fid)))
    print('wrote %d this run; total %d/%d'%(done,have,tot))
if __name__=='__main__': main()
