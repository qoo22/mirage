#!/usr/bin/env python3
# 全職業の in-dungeon スプライト(front/side idle)を HF データセットにアップロード。
# hf auth login 済みのローカル環境で実行する想定。
import os
from huggingface_hub import HfApi
ASSETS='/Users/takaaki/Desktop/桜井政博ゲームプロジェクト/04_ゲーム/ミラージュゲート桜井/assets'
REPO='takaokkkk/mirage-hero-anim'
JOBS=['warrior','wizard','gambler','ren','samurai','dragoon','paladin','sorcerer',
      'darkknight','fallen','reddragoon','gilgamesh','lucifer','odin','loki',
      'valkyrie','magicblade','crimson','verdant','ranger']
api=HfApi(); api.create_repo(REPO, repo_type='dataset', exist_ok=True)
n=0
for j in JOBS:
    for view in ['front','side']:
        src=os.path.join(ASSETS, 't_d_%s_%s_idle.png'%(j,view))
        if os.path.exists(src):
            api.upload_file(path_or_fileobj=src, path_in_repo='src/%s_%s.png'%(j,view),
                            repo_id=REPO, repo_type='dataset')
            n+=1; print('uploaded', j, view, flush=True)
print('UPLOADED', n, 'files', flush=True)
