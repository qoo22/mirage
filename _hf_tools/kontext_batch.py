# HFジョブとして実行: data set src/<job>_front.png から各職業の「剣を掲げる」掲げ絵を
# FLUX.1-Kontext で忠実生成（絵柄・キャラ保持）→白背景を透過に戻す→シート/GIF/透過PNGを生成。
import io
from huggingface_hub import InferenceClient, HfApi, hf_hub_download
from PIL import Image
from collections import deque
REPO='takaokkkk/mirage-hero-anim'
api=HfApi(); c=InferenceClient(); M='black-forest-labs/FLUX.1-Kontext-dev'
JOBS=['warrior','wizard','gambler','ren','samurai','dragoon','paladin','sorcerer',
      'darkknight','fallen','reddragoon','gilgamesh','lucifer','odin','loki',
      'valkyrie','magicblade','crimson','verdant','ranger']
def to_bytes(im):
    b=io.BytesIO(); im.save(b,'PNG'); return b.getvalue()
def edit(b, instr):
    return c.image_to_image(b, prompt=instr, model=M).convert('RGB')
def cut_white(im, thr=236):
    im=im.convert('RGBA'); w,h=im.size; px=im.load()
    seen=[[False]*w for _ in range(h)]; dq=deque()
    for x in range(w): dq.append((x,0)); dq.append((x,h-1))
    for y in range(h): dq.append((0,y)); dq.append((w-1,y))
    while dq:
        x,y=dq.popleft()
        if x<0 or y<0 or x>=w or y>=h or seen[y][x]: continue
        seen[y][x]=True; r,g,b,a=px[x,y]
        if r>=thr and g>=thr and b>=thr:
            px[x,y]=(r,g,b,0); dq.extend([(x+1,y),(x-1,y),(x,y+1),(x,y-1)])
    return im
RAISE_MID='The same chibi RPG hero character lifts the weapon upward with both hands, arms rising, starting to raise it overhead. Keep the EXACT same character, equipment, colors, helmet/hair, cape and cute art style. Plain solid white background. Full body, centered, no text.'
RAISE_TOP='The same chibi RPG hero character holds the weapon straight up high above the head with both arms fully extended, triumphant equip pose, the weapon shining with bright golden light. Keep the EXACT same character, equipment, colors, helmet/hair, cape and cute art style. Plain solid white background. Full body, centered, no text.'
ok=[]
for j in JOBS:
    try:
        p=hf_hub_download(REPO,'src/%s_front.png'%j,repo_type='dataset')
    except Exception as e:
        print('SKIP',j,'no src',flush=True); continue
    src=Image.open(p).convert('RGBA'); S=src.size
    bg=Image.new('RGBA',S,(255,255,255,255)); bg.alpha_composite(src); sb=to_bytes(bg.convert('RGB'))
    try:
        f1=cut_white(edit(sb,RAISE_MID).resize(S))
        f2=cut_white(edit(sb,RAISE_TOP).resize(S))
    except Exception as e:
        print('FAIL',j,type(e).__name__,str(e)[:120],flush=True); continue
    api.upload_file(path_or_fileobj=to_bytes(f1),path_in_repo='t_d_%s_front_raisemid.png'%j,repo_id=REPO,repo_type='dataset')
    api.upload_file(path_or_fileobj=to_bytes(f2),path_in_repo='t_d_%s_front_raise.png'%j,repo_id=REPO,repo_type='dataset')
    W,H=S; sheet=Image.new('RGBA',(W*3,H),(0,0,0,0))
    for i,im in enumerate([src,f1,f2]): sheet.alpha_composite(im,(i*W,0))
    api.upload_file(path_or_fileobj=to_bytes(sheet),path_in_repo='%s_equip_sheet.png'%j,repo_id=REPO,repo_type='dataset')
    def onwhite(im):
        b=Image.new('RGBA',S,(255,255,255,255)); b.alpha_composite(im); return b.convert('P',palette=Image.ADAPTIVE)
    frs=[onwhite(x) for x in [src,f1,f2,f2,f1]]
    g=io.BytesIO(); frs[0].save(g,'GIF',save_all=True,append_images=frs[1:],duration=160,loop=0,disposal=2)
    api.upload_file(path_or_fileobj=g.getvalue(),path_in_repo='%s_equip.gif'%j,repo_id=REPO,repo_type='dataset')
    ok.append(j); print('DONE',j,flush=True)
print('ALL DONE jobs=',len(ok),ok,flush=True)
