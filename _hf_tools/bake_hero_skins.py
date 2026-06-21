#!/usr/bin/env python3
# ヒーローのカラースキンを静的PNGに焼き出す（HF不要・ローカル Pillow+numpy・高速ベクトル化）。
# (b)マスク方式：暖色の肌色を据え置き、装備/服だけ HSV 色相回転＝「顔は元のまま」。
# 出力: assets/hero_skins/<finish>/<元のファイル名>   （ゲームは存在すればこれを優先表示）
# 使い方:
#   python3 _hf_tools/bake_hero_skins.py warrior
#   python3 _hf_tools/bake_hero_skins.py warrior,wizard,valkyrie
#   python3 _hf_tools/bake_hero_skins.py all
import os, sys
import numpy as np
from PIL import Image

ASSETS=os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),'assets')
OUT=os.path.join(ASSETS,'hero_skins')
ALL_JOBS=['warrior','wizard','gambler','ren','samurai','dragoon','paladin','sorcerer',
          'darkknight','fallen','reddragoon','gilgamesh','lucifer','odin','loki','valkyrie','magicblade']
# finish -> (色相回転deg, 彩度倍, 明度倍)。azure=原色 / prismatic=虹アニメ＝実行時専用 なので焼かない。
FIN={'crimson':(150,1.18,1.0),'verdant':(80,1.10,1.0),'golden':(200,1.22,1.0),
     'violet':(55,1.12,1.0),'aqua':(175,1.05,1.0),'inferno':(165,1.30,1.0),
     'shadow':(0,0.45,0.72),
     # --- 追加レアスキン（顔据え置きマスク・装備のみ再配色）---
     'rose':(128,1.05,1.10),      # 桃/ローズ
     'platinum':(0,0.30,1.25),    # 白銀/プラチナ
     'obsidian':(210,0.35,0.40),  # 漆黒/オブシディアン
     'emerald':(270,1.28,1.05),   # 翠/エメラルド
     'sapphire':(30,1.35,0.92),   # 蒼藍/サファイア
     'frost':(330,0.72,1.20),     # 氷/フロスト
     'bloodmoon':(120,1.45,0.80), # 暗紅/ブラッドムーン
     'venom':(250,1.55,1.05),     # 毒緑/ヴェノム
     'sakura':(92,0.78,1.20)}     # 淡桜/サクラ

def recolor_np(im, hue_deg, sat_mul, br_mul):
    rgba=np.asarray(im.convert('RGBA'), dtype=np.uint8)
    a=rgba[...,3]
    hsv=np.asarray(Image.fromarray(rgba[...,:3],'RGB').convert('HSV'), dtype=np.float32)  # H,S,V 0-255
    H,S,V=hsv[...,0],hsv[...,1],hsv[...,2]
    # 肌マスク：暖色 hue~5-33(=7-47deg) / 中彩度 / 明るめ
    skin=(H>=5)&(H<=33)&(S>=38)&(S<=199)&(V>=76)
    target=(a>0)&(~skin)
    Hn=(H + (hue_deg/360.0*255.0)) % 256.0
    Sn=np.clip(S*sat_mul,0,255); Vn=np.clip(V*br_mul,0,255)
    H[target]=Hn[target]; S[target]=Sn[target]; V[target]=Vn[target]
    out=np.stack([H,S,V],axis=-1).astype(np.uint8)
    rgb=np.asarray(Image.fromarray(out,'HSV').convert('RGB'),dtype=np.uint8)
    res=np.dstack([rgb,a])
    return Image.fromarray(res,'RGBA')

def main():
    arg=(sys.argv[1] if len(sys.argv)>1 else 'warrior')
    jobs=ALL_JOBS if arg=='all' else arg.split(',')
    files=sorted(os.listdir(ASSETS))
    total=0
    for fid,(hue,sm,bm) in FIN.items():
        d=os.path.join(OUT,fid); os.makedirs(d,exist_ok=True)
        for j in jobs:
            slug='wizard' if j=='mage' else j
            pre='t_d_%s_'%slug
            for fn in files:
                if fn.startswith(pre) and fn.endswith('.png'):
                    recolor_np(Image.open(os.path.join(ASSETS,fn)),hue,sm,bm).save(os.path.join(d,fn))
                    total+=1
        print('baked finish=%s'%fid,flush=True)
    print('DONE total=%d -> %s'%(total,OUT))

if __name__=='__main__': main()
