"""Original instrumental scores and deterministic synthesizer for the BBSB stage pack.
No sampled songs or third-party audio are used. MIDI, cue metadata and OGG share one score.
Python 3 + numpy + scipy, and ffmpeg on PATH. Output is deliberately outside the repository.
"""
import argparse, hashlib, json, math, struct, subprocess, wave
from functools import lru_cache
from pathlib import Path
import numpy as np
from scipy.signal import lfilter, butter, sosfilt
from build_catalog import sections

RATE=44100
GM={'piano':0,'rhodes':4,'organ':16,'bell':10,'vibes':11,'marimba':12,'lute':24,'acoustic':25,'clean':27,'muted':28,'guitar':30,'bass':33,'sub':38,'fiddle':40,'strings':48,'brass':61,'flute':73,'whistle':78,'square':80,'saw':81,'pad':89,'harp':46}
# Ten separately authored melodic contours. Genre registers, modes, answering phrases and rhythms
# change their reading; a song's hook returns recognizably three times.
MOTIFS={
'POP':[[0,2,4,4,5,4,2,1],[4,2,1,2,0,2,4,5],[0,0,4,5,4,2,1,2],[2,4,5,4,2,1,0,2],[4,6,7,6,4,2,4,5],[0,4,2,5,4,2,1,0],[0,2,3,4,5,4,2,4],[4,2,0,1,2,4,6,4],[0,1,2,4,2,5,4,7],[0,4,5,7,6,4,2,0]],
'RNB':[[2,4,6,5,4,2,1,0],[4,3,2,1,0,2,4,6],[0,2,4,3,2,1,-1,0],[6,4,3,4,2,1,0,2],[2,1,0,-1,0,2,3,4],[4,6,5,3,4,2,0,1],[0,2,3,6,5,4,2,1],[3,4,6,4,2,3,1,0],[2,4,5,6,4,3,2,0],[0,3,4,6,7,6,4,2]],
'JAZZ':[[2,4,6,5,3,4,1,0],[0,2,5,4,3,1,2,0],[4,6,5,4,2,3,1,-1],[1,2,4,6,5,3,2,0],[0,3,4,6,4,2,1,3],[4,3,2,4,6,7,5,4],[0,2,3,4,3,2,-1,0],[2,4,7,6,5,3,4,2],[4,5,6,4,2,1,-1,0],[0,2,4,6,7,5,3,4]],
'RAP':[[0,0,2,3,2,0,-1,0],[4,4,2,0,-1,0,2,1],[0,3,2,0,4,3,2,0],[2,4,3,2,0,-1,0,2],[0,0,-1,0,3,2,1,0],[4,2,0,2,3,2,0,-1],[0,2,4,3,2,0,2,1],[2,0,2,4,3,2,0,3],[0,-1,0,2,3,4,2,0],[0,3,4,2,0,2,3,4]],
'BARD':[[0,1,2,4,3,2,1,0],[2,0,1,2,4,3,1,0],[0,2,4,5,4,2,3,1],[4,4,2,3,1,2,0,1],[0,2,1,3,2,4,3,1],[4,3,2,0,1,2,4,3],[2,3,4,6,5,4,2,0],[0,1,3,2,4,3,1,0],[0,4,3,2,1,2,4,5],[0,2,4,7,6,4,3,2]],
'CELT':[[0,2,4,3,2,1,2,0],[4,2,1,0,2,4,5,4],[2,3,4,6,5,4,2,1],[0,1,2,4,5,4,3,2],[0,2,3,2,1,0,-1,0],[4,5,4,2,3,2,1,0],[2,4,5,4,2,0,1,2],[0,4,5,7,5,4,2,1],[2,4,6,5,4,3,2,0],[0,2,4,5,7,6,4,2]],
'NEW':[[0,4,2,1,0,2,4,7],[2,4,6,4,2,0,1,2],[0,2,4,5,4,2,1,0],[4,2,0,2,4,5,7,6],[0,3,4,6,4,3,1,0],[2,4,5,4,2,1,0,2],[0,4,7,6,4,2,1,0],[4,5,7,4,2,4,1,0],[2,4,6,7,6,4,2,1],[0,2,4,7,6,4,2,0]],
'METAL':[[0,0,1,0,4,3,1,0],[0,2,0,3,2,1,0,-1],[4,3,2,0,1,0,3,2],[0,1,0,4,3,2,1,0],[0,4,5,4,3,1,2,0],[0,0,-1,0,2,1,-1,0],[0,2,3,4,3,1,0,2],[4,6,7,6,4,3,2,0],[0,1,3,2,0,4,3,1],[0,4,3,2,1,0,-1,0]]}
LEADS={
'POP':['saw','square','square','whistle','brass','bell','brass','saw','bell','saw'],
'RNB':['rhodes','clean','vibes','flute','clean','bell','rhodes','brass','vibes','rhodes'],
'JAZZ':['brass','piano','brass','vibes','clean','organ','brass','piano','brass','brass'],
'RAP':['piano','brass','square','organ','bell','marimba','brass','clean','strings','brass'],
'BARD':['flute','lute','flute','brass','lute','harp','strings','flute','brass','flute'],
'CELT':['whistle','fiddle','harp','whistle','flute','fiddle','whistle','fiddle','harp','whistle'],
'NEW':['piano','bell','harp','marimba','bell','flute','piano','harp','pad','piano'],
'METAL':['guitar','guitar','guitar','guitar','guitar','guitar','guitar','organ','guitar','guitar']}
MAJOR=[0,2,4,5,7,9,11]; MINOR=[0,2,3,5,7,8,10]; DORIAN=[0,2,3,5,7,9,10]; PENTA=[0,2,4,7,9,12,14]
KEYS=[0,2,7,5,9,3,10,4,1,7]
PROGS=[[0,5,3,4],[5,3,0,4],[0,4,5,3],[3,0,4,5],[0,3,5,4],[5,4,3,4],[0,5,1,4],[3,4,2,5],[0,2,3,4],[0,3,4,0]]

def midi_var(n):
    b=[n&127];n>>=7
    while n:b.insert(0,(n&127)|128);n>>=7
    return bytes(b)
def midi_chunk(events):
    out=bytearray();prev=0
    for tick,priority,data in sorted(events,key=lambda e:(e[0],e[1])):
        out+=midi_var(tick-prev)+data;prev=tick
    out+=b'\x00\xff\x2f\x00'
    return b'MTrk'+struct.pack('>I',len(out))+out

def write_midi(path,notes,parts,stage,secs):
    factor=1.5 if stage['meter']=='6/8' else 1
    ppq=480;tempo=round(60000000/(stage['bpm']*factor))
    num,den=map(int,stage['meter'].split('/'))
    events=[(0,0,b'\xff\x51\x03'+tempo.to_bytes(3,'big')),(0,1,bytes([255,88,4,num,int(math.log2(den)),36 if factor==1.5 else 24,8]))]
    beats=2 if factor==1.5 else num
    for s in secs:
        label=s['name'].encode();events.append((round(s['start_bar']*beats*factor*ppq),2,b'\xff\x06'+midi_var(len(label))+label))
    events.append((round(stage['bars']*beats*factor*ppq),3,b'\xff\x06\x03END'))
    tracks=[midi_chunk(events)]
    for ch,(name,program,pan,level) in parts.items():
        label=name.encode();e=[(0,0,b'\xff\x03'+midi_var(len(label))+label),(0,1,bytes([0xc0|ch,program])),(0,2,bytes([0xb0|ch,10,int((pan+1)*63.5)]))]
        for n in notes:
            if n[0]!=ch:continue
            _,t,d,p,v=n
            e.append((round(t*factor*ppq),4,bytes([0x90|ch,p,v])))
            e.append((round((t+d)*factor*ppq),3,bytes([0x80|ch,p,0])))
        tracks.append(midi_chunk(e))
    path.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(tracks),ppq)+b''.join(tracks))

def score(stage):
    genre=stage['map_id'];idx=stage['index']-1
    compound=stage['meter']=='6/8';beats=2 if compound else int(stage['meter'][0]);total=stage['bars']*beats
    scale=(MINOR if genre in ['RAP','METAL'] else DORIAN if genre in ['RNB','JAZZ'] or (genre=='CELT' and idx%3==1) else MAJOR)
    key=KEYS[idx]+(2 if genre=='CELT' else 0)
    lead=LEADS[genre][idx]
    chord={'POP':'clean' if idx in [1,3,4] else 'pad','RNB':'rhodes','JAZZ':'piano','RAP':'rhodes','BARD':'lute','CELT':'acoustic','NEW':'pad','METAL':'guitar'}[genre]
    answer={'POP':'bell','RNB':'clean','JAZZ':'vibes','RAP':'bell','BARD':'flute','CELT':'harp','NEW':'bell','METAL':'strings'}[genre]
    arp={'POP':'square','RNB':'vibes','JAZZ':'clean','RAP':'muted','BARD':'harp','CELT':'harp','NEW':'harp','METAL':'guitar'}[genre]
    bass='sub' if genre=='RAP' and idx>=4 else 'bass'
    parts={0:(lead,GM[lead],-.12,.19),1:(chord,GM[chord],-.36,.115),2:(bass,GM[bass],0,.28),3:(arp,GM[arp],.44,.075),4:(answer,GM[answer],.25,.12),5:('pad',89,.08,.052),9:('drums',0,0,.42)}
    if genre=='METAL': parts[0]=(lead,GM[lead],-.1,.15);parts[1]=(chord,GM[chord],-.45,.23);parts[3]=(arp,GM[arp],.45,.17)
    if genre=='NEW': parts[9]=('drums',0,0,.20)
    if genre in ['CELT','BARD']:parts[9]=('drums',0,0,.28)
    secs=sections(stage['bars']);notes=[]
    def pitch(degree,octave): return octave*12+key+scale[degree%7]+12*(degree//7)
    def add(ch,t,d,p,v=85):
        if t>=total-.04:return
        notes.append((ch,round(t,7),round(min(d,total-t-.01),7),min(115,max(24,int(p))),min(120,max(24,int(v)))))
    def drum(t,p,v=90):add(9,t,.12,p,v)
    prog=PROGS[idx]
    if genre=='JAZZ':prog=[[1,4,0,5],[0,5,1,4],[3,6,2,4],[1,4,0,0]][idx%4]
    if genre=='METAL':prog=[[0,0,5,4],[0,6,5,4],[0,2,3,4]][idx%3]
    motif=MOTIFS[genre][idx]
    hooks=[s['start_bar'] for s in secs if s['is_hook']]
    for bar in range(stage['bars']):
        sec=next(s for s in secs if s['start_bar']<=bar<s['start_bar']+s['bars'])
        hook=sec['is_hook'];intro=bar<2;outro=bar>=stage['bars']-2
        before_hook=bar+1 in hooks
        b=bar*beats;root=prog[bar%4];energy=1 if hook else .80 if not intro else .60
        if outro:energy=.62
        # Every main beat has a short percussive anchor, including breakdowns.
        for beat in range(beats):
            at=b+beat
            drum(at,36 if genre not in ['BARD','CELT','NEW'] else 41,90*energy if beat==0 else 62*energy)
            if genre in ['BARD','CELT']:
                drum(at,54,57*energy)
                for k in [1,2] if compound else [1]:drum(at+k/(3 if compound else 2),42,38*energy)
            elif genre=='NEW':drum(at,76,48*energy);drum(at+.5,42,30)
            elif genre=='JAZZ':
                drum(at,51,70*energy);drum(at+2/3,51,45*energy)
                if beat%2==1 or (beats==3 and beat==2):drum(at,37,66*energy)
            else:
                hatstep=.25 if (genre=='RAP' and idx>=4) or (genre=='METAL' and hook) else .5
                for step in np.arange(0,1,hatstep):drum(at+float(step),42,54*energy if step==0 else 37*energy)
                if beat%2==1:drum(at,38 if genre!='RAP' else 39,100*energy)
        if genre in ['POP','RNB','RAP'] and not intro and beats==4:
            for pos in ([1.5,2.75] if genre in ['RNB','RAP'] else [2.5]):drum(b+pos,36,72*energy)
        if genre=='METAL' and not intro:
            for pos in np.arange(0,beats,.5 if not hook else .25):drum(b+float(pos),36,72*energy)
        if before_hook:
            for j in range(4):drum(b+beats-1+j*.25,38 if genre not in ['BARD','CELT','NEW'] else 45,52+j*9)
        if hook and bar==sec['start_bar']:drum(b,49,80)
        # Bass follows the kick and harmony. Jazz uses a separate walking line.
        if genre=='JAZZ':
            for k in range(beats):add(2,b+k,.83,pitch(root+[0,2,4,6][k%4],3),75+6*(k==0))
        elif genre in ['CELT','BARD']:
            for k in range(beats):add(2,b+k,.65,pitch(root+(4 if k%2 else 0),3),70*energy)
        elif genre=='NEW':
            add(2,b,beats*.88,pitch(root,3),62*energy)
        else:
            for pos in ([0,1.5,2,3.5] if beats==4 else range(beats)):
                add(2,b+pos,.42 if genre=='METAL' else .68,pitch(root+(7 if genre=='POP' and idx==4 and pos%2 else 0),3),83*energy)
        # Harmonic bed: extended harmony for R&B/jazz, open fifths for metal.
        degrees=[root,root+4] if genre=='METAL' else [root,root+2,root+4]+([root+6] if genre in ['RNB','JAZZ'] else [])
        if genre=='RNB' and idx%2:degrees.append(root+8)
        if genre=='METAL':
            for k,pos in enumerate(np.arange(0,beats,.5)):
                riff=motif[(bar*4+k)%8] if hook else root+(0 if k%4<3 else 1)
                for degree in [riff,riff+4]:
                    add(1,b+float(pos),.20 if not hook else .30,pitch(degree,3),90*energy)
                    add(3,b+float(pos)+.007,.22,pitch(degree,3),78*energy)
        else:
            positions=[0] if genre=='NEW' else ([.0,beats*.5+.12] if genre in ['RNB','JAZZ'] else [0,beats/2])
            for pos in positions:
                for j,degree in enumerate(degrees):add(1,b+pos+j*(.016 if chord in ['acoustic','lute','clean'] else .008),beats*.44,pitch(degree,4),66*energy)
            if not intro:
                sub=1/3 if compound else .5
                for k,pos in enumerate(np.arange(0,beats,sub)):
                    if genre in ['RNB','RAP'] and k%2:continue
                    add(3,b+float(pos),sub*.72,pitch(root+[0,4,2,7][(k+idx)%4],5),50*energy)
        if hook or (genre=='NEW' and not intro):
            for deg in [root,root+4,root+7]:add(5,b,beats*.93,pitch(deg,4),57 if hook else 38)
        # Hook uses the full contour. Verses leave room and develop its first and last notes.
        if hook:
            relbar=bar-sec['start_bar'];phrase=relbar%2
            if compound:
                rhythm=[0,1/3,2/3,1,4/3,5/3];which=[0,1,2,3,4,5]
            elif beats==3:
                rhythm=[0,.5,1,1.5,2,2.5];which=[0,1,2,3,4,5]
            else:
                rhythm=[0,.5,1.5,2,3];which=[0,1,2,3,4]
            for k,pos in enumerate(rhythm):
                degree=motif[(k+phrase*4)%8]
                if relbar==3 and k==len(rhythm)-1:degree=0 if idx%2==0 else 4
                dur=(rhythm[k+1]-pos)*.78 if k+1<len(rhythm) else (beats-pos)*.78
                swing=.08 if genre=='RNB' and pos%1 else (.16 if genre=='JAZZ' and pos%1 else 0)
                add(0,b+pos+swing,dur,pitch(degree,5 if genre!='METAL' else 4),96 if k==0 else 86)
            if relbar%2==1:
                add(4,b+beats-.5,.40,pitch(motif[(relbar+idx)%8],6 if genre in ['CELT','NEW'] else 5),67)
        elif not intro and not outro:
            for k,pos in enumerate([0,beats*.6] if bar%2==0 else [beats*.35]):
                degree=motif[(bar+k*3)%8]
                add(0,b+pos,min(.75,beats-pos-.03),pitch(degree,4 if genre in ['RNB','RAP','METAL'] else 5),63)
            if bar%4==3:add(4,b+beats-.5,.38,pitch(motif[(bar+1)%8],5),62)
        elif intro and bar==1:
            for k,pos in enumerate([0,beats/2]):add(4,b+pos,.45,pitch(motif[k*2],5),59)
        if outro and bar==stage['bars']-2:
            for degree in [0,2,4]:add(0,b,beats*1.7,pitch(degree,5),66)
    return notes,parts,secs

@lru_cache(maxsize=4096)
def synth(name,pitch,duration_ms):
    duration=duration_ms/1000; release=.07 if name in ['guitar','muted','bass','sub'] else .19 if name in ['flute','whistle','brass','square','saw','fiddle','strings','pad'] else .36
    n=round((duration+release)*RATE);t=np.arange(n,dtype=np.float32)/RATE
    f=440*2**((pitch-69)/12);phase=2*np.pi*f*t
    env=np.minimum(1,t/(.025 if name in ['pad','strings'] else .011 if name in ['flute','fiddle'] else .003))
    env*=np.where(t<=duration,1,np.maximum(0,1-(t-duration)/release)**2)
    if name in ['piano','rhodes','bell','vibes','marimba']:
        if name=='rhodes':x=np.sin(phase+1.25*np.sin(phase*2)*np.exp(-t*3))+.17*np.sin(phase*3)*np.exp(-t*5)
        elif name in ['bell','vibes']:x=np.sin(phase)+.40*np.sin(phase*2.76)*np.exp(-t*3)+.18*np.sin(phase*5.4)*np.exp(-t*6)
        elif name=='marimba':x=np.sin(phase)+.48*np.sin(phase*4)*np.exp(-t*13)
        else:x=sum((1/k**1.35)*np.sin(phase*k+(.3 if k%2 else 0))*np.exp(-t*(.6+k*.38)) for k in range(1,9))
        env*=np.exp(-t*(3.4 if name=='marimba' else 1.6 if name=='piano' else 1.2))
    elif name in ['lute','acoustic','clean','muted','harp','guitar']:
        harmonics=range(1,min(18,int(RATE*.43/f)))
        x=sum(np.sin(phase*k+.018*k*k)*(math.sin(k*.63)/k**1.18)*np.exp(-t*(1.8+k*.58)) for k in harmonics)
        if name=='guitar':x=np.tanh(x*5.5);x=sosfilt(butter(2,5200,fs=RATE,output='sos'),x)
        if name=='muted':env*=np.exp(-t*15)
        elif name=='lute':env*=np.exp(-t*2.1)
        elif name=='harp':env*=np.exp(-t*.8)
        else:env*=np.exp(-t*1.25)
    elif name in ['bass','sub']:
        x=np.sin(phase)+(.3 if name=='bass' else .08)*np.sin(phase*2)+.10*np.sin(phase*3);env*=np.exp(-t*1.4)
    elif name in ['flute','whistle']:
        vibr=phase+.025*np.sin(2*np.pi*5.1*t)*np.minimum(1,t/.15)
        x=np.sin(vibr)+(.22 if name=='flute' else .07)*np.sin(vibr*2)+.045*np.sin(vibr*3)
    else:
        x=np.zeros(n,dtype=np.float32)
        maxk=min(18,int(RATE*.42/f))
        for k in range(1,maxk):
            if name=='square' and k%2==0:continue
            if name=='organ' and k not in [1,2,3,4,6,8]:continue
            amp=(1/k**1.4 if name in ['pad','strings'] else 1/k**1.1)*np.exp(-k*(.045 if name in ['brass','saw','fiddle'] else .12))
            vibr=.009 if name in ['saw','pad','strings','fiddle'] else .004
            x+=amp*np.sin(phase*k+vibr*k*np.sin(2*np.pi*4.7*t))
        if name=='pad':env*=np.minimum(1,t/.09)
        if name=='brass':env*=.75+.25*np.exp(-t*8)
    x=np.asarray(x*env,dtype=np.float32)
    peak=np.max(np.abs(x));return x/(max(peak,.2)*1.2)

@lru_cache(maxsize=100)
def percussion(pitch):
    length={36:.36,38:.25,39:.20,42:.095,49:.95,51:.27,54:.12,41:.30,45:.24,76:.055}.get(pitch,.2)
    t=np.arange(round(length*RATE),dtype=np.float32)/RATE
    rng=np.random.default_rng(pitch*913);noise=rng.uniform(-1,1,len(t)).astype(np.float32)
    if pitch==36:
        x=np.sin(2*np.pi*(48*t+5*(1-np.exp(-t*42))))*np.exp(-t*14)+noise*.19*np.exp(-t*160)
    elif pitch in [41,45]:
        x=np.sin(2*np.pi*(92 if pitch==41 else 145)*t)*np.exp(-t*15)+noise*.12*np.exp(-t*60)
    elif pitch in [38,39]:
        high=noise-lfilter([.08],[1,-.92],noise)
        x=high*np.exp(-t*26)*.76+np.sin(2*np.pi*183*t)*np.exp(-t*37)*.36
        if pitch==39:x*=.5+.5*np.cos(2*np.pi*95*t)**2
    elif pitch==76:x=np.sin(2*np.pi*1120*t)*np.exp(-t*105)+np.sin(2*np.pi*1640*t)*np.exp(-t*125)*.35
    else:
        high=noise-lfilter([.22],[1,-.78],noise)
        metal=sum(np.sin(2*np.pi*f*t) for f in [3431,4777,6137,7241])/4
        x=(high*.60+metal*.30)*np.exp(-t*(50 if pitch==42 else 30 if pitch==54 else 13 if pitch==51 else 5))
    x*=np.minimum(1,t/.0005);return np.asarray(x,dtype=np.float32)

def render(stage,output):
    dest=output/'Assets/BBSB/Resources/BBSB/StageAssets'/stage['id'];dest.mkdir(parents=True,exist_ok=True)
    sources=output/'StageAssetSources'/stage['id'];sources.mkdir(parents=True,exist_ok=True)
    notes,parts,secs=score(stage);beat=60/stage['bpm'];duration=80
    mix=np.zeros((duration*RATE,2),dtype=np.float32)
    for ch,t,d,p,v in notes:
        name,program,pan,level=parts[ch]
        sample=percussion(p) if ch==9 else synth(name,p,max(20,round(d*beat*1000)))
        start=round(t*beat*RATE);size=min(len(sample),len(mix)-start)
        if size<=0:continue
        amp=(v/100)**1.5*level
        if ch==9:pan=-.15 if p==42 else .25 if p in [49,51,54] else 0
        gains=np.array([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)],dtype=np.float32)
        mix[start:start+size]+=sample[:size,None]*gains*amp
    # A short stereo room, preserving the dry beat attack. No leading silence is introduced.
    for delay,gain in [(0.043,.13),(.089,.085),(.137,.045)]:
        shift=round(delay*RATE);mix[shift:,0]+=mix[:-shift,1].copy()*gain;mix[shift:,1]+=mix[:-shift,0].copy()*gain
    mix=sosfilt(butter(2,28,fs=RATE,btype='highpass',output='sos'),mix,axis=0).astype(np.float32)
    tail=round(.25*RATE);mix[-tail:]*=np.linspace(1,0,tail,dtype=np.float32)[:,None]
    mix=np.tanh(mix*1.25);mix*=.91/max(float(np.max(np.abs(mix))),.01)
    wav=sources/'render-temp.wav'
    with wave.open(str(wav),'wb') as f:
        f.setnchannels(2);f.setsampwidth(2);f.setframerate(RATE)
        pcm=(mix*32767).astype('<i2')
        for start in range(0,len(pcm),262144): f.writeframes(pcm[start:start+262144].tobytes())
    with wave.open(str(wav),'rb') as check:
        assert check.getnframes()==duration*RATE
        assert len(check.readframes(duration*RATE))==duration*RATE*4
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(wav),'-af','loudnorm=I=-16:TP=-1.5:LRA=9','-ar',str(RATE),'-c:a','libvorbis','-q:a','6','-metadata','title='+stage['name'],'-metadata','album=BBSB '+stage['map_id'],'-metadata','artist=BBSB Original Stage Score',str(dest/'music.ogg')],check=True)
    probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_entries','stream=duration_ts','-of','json',str(dest/'music.ogg')]))
    assert probe['streams'][0]['duration_ts']==duration*RATE, stage['id']+' audio render length mismatch'
    wav.unlink()
    write_midi(sources/'score.mid',notes,parts,stage,secs)
    cues=dict(stage_id=stage['id'],name=stage['name'],map_id=stage['map_id'],bpm=stage['bpm'],bpm_unit=stage['bpm_unit'],meter=stage['meter'],bars=stage['bars'],duration_seconds=80,audio_offset_seconds=0,sample_rate=RATE,composition='Original authored instrumental score; deterministic synthesized instruments; no third-party samples.',instruments={str(k):v[0] for k,v in parts.items()},sections=secs,hooks=[dict(index=i+1,start_bar=s['start_bar'],bars=s['bars'],start_seconds=s['start_bar']*(2 if stage['meter']=='6/8' else int(stage['meter'][0]))*beat,end_seconds=(s['start_bar']+s['bars'])*(2 if stage['meter']=='6/8' else int(stage['meter'][0]))*beat,requires_monster_response=True) for i,s in enumerate(s for s in secs if s['is_hook'])],note_events=len(notes),audio_sha256=hashlib.sha256((dest/'music.ogg').read_bytes()).hexdigest())
    (sources/'cues.json').write_text(json.dumps(cues,ensure_ascii=False,indent=2))
    (sources/'score.json').write_text(json.dumps(dict(parts=parts,notes=notes),ensure_ascii=False,separators=(',',':')))
    print(stage['id'],len(notes),'notes',dest.joinpath('music.ogg').stat().st_size,flush=True)
    synth.cache_clear()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);parser.add_argument('--map');parser.add_argument('--stage');parser.add_argument('--overwrite',action='store_true');args=parser.parse_args()
    data=json.loads((Path(__file__).parent/'stage_concepts.json').read_text());out=Path(args.output).resolve()
    for m in data['maps']:
        if args.map and m['id']!=args.map:continue
        for s in m['stages']:
            if args.stage and s['id']!=args.stage:continue
            if not args.overwrite and (out/'StageAssetSources'/s['id']/'cues.json').exists():continue
            render(s,out)
if __name__=='__main__':main()
