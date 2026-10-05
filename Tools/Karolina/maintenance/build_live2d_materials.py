"""Build Karolina's first material-separation PSD without changing approved art.

Pixel editing was explicitly authorized by the user on 2026-10-04. Source pixels
are assigned once to anatomical parts; hidden repairs are separately recorded.
This prepares artwork, not a Cubism rig. Uses the already installed Pillow.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import struct
from pathlib import Path

from PIL import Image, ImageChops, ImageCms, ImageDraw, ImageFilter, ImageFont

SIZE = (1024, 1536)
ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Tools/Karolina/Design/KarolinaCharacter/Live2D"
SOURCE = ART / "Candidates/karolina-front-master-v001.png"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def polygon(points: list[tuple[int, int]]) -> Image.Image:
    result = Image.new("L", SIZE)
    ImageDraw.Draw(result).polygon(points, fill=255)
    return result


def rectangle(box: tuple[int, int, int, int]) -> Image.Image:
    result = Image.new("L", SIZE)
    ImageDraw.Draw(result).rectangle((box[0], box[1], box[2]-1, box[3]-1), fill=255)
    return result


def ellipse(box: tuple[int, int, int, int]) -> Image.Image:
    result = Image.new("L", SIZE)
    ImageDraw.Draw(result).ellipse(box, fill=255)
    return result


def union(*masks: Image.Image) -> Image.Image:
    result = Image.new("L", SIZE)
    for mask in masks:
        result = ImageChops.lighter(result, mask)
    return result


def intersection(*masks: Image.Image) -> Image.Image:
    result = Image.new("L", SIZE, 255)
    for mask in masks:
        result = ImageChops.darker(result, mask)
    return result


def cut(source: Image.Image, mask: Image.Image, normalize: bool = False) -> Image.Image:
    alpha = source.getchannel("A")
    if normalize:
        alpha = alpha.point(lambda value: 255 if value >= 240 else value)
    alpha = ImageChops.multiply(alpha, mask)
    support = alpha.point(lambda value: 255 if value else 0)
    result = Image.composite(source, Image.new("RGBA", SIZE), support)
    result.putalpha(alpha)
    return result


def anatomical_specs(source: Image.Image) -> list[tuple[str, str, Image.Image]]:
    """Priority order: first claimant owns each approved source pixel."""
    eye_vl = polygon([(450,184),(456,177),(466,175),(479,175),(489,178),
                      (497,184),(491,192),(483,198),(474,201),(463,198),(454,193)])
    eye_vr = polygon([(523,177),(530,168),(541,165),(554,166),(566,171),
                      (564,181),(558,187),(548,194),(537,192),(529,186)])
    lashes_vl = polygon([(447,186),(450,179),(454,177),(458,173),(461,173),
                         (464,175),(469,173),(477,174),(486,177),(493,181),
                         (499,186),(491,183),(482,180),(471,179),(462,181),
                         (456,185),(453,190)])
    lashes_vr = polygon([(522,179),(525,173),(530,169),(535,166),(544,164),
                         (556,165),(565,169),(574,172),(567,175),(556,172),
                         (545,171),(535,173),(528,177)])
    face = polygon([(457,195),(450,192),(449,199),(455,210),(464,218),
                    (477,229),(495,241),(514,252),(540,244),(560,232),
                    (572,219),(581,207),(592,201),(600,190),(602,180),
                    (597,170),(590,168),(586,171),(580,178),(577,188),
                    (574,204),(567,216),(557,227),(563,211),(571,195),
                    (576,176),(568,166),(560,152),(553,140),(548,123),
                    (543,138),(535,151),(523,165),(511,177),(500,187),
                    (490,180),(478,185)])
    skin = Image.new("L", SIZE)
    skin.putdata([255 if r > g+9 and r > b+3 and r > 90 else 0
                  for r,g,b,a in source.get_flattened_data()])
    skin = skin.filter(ImageFilter.MaxFilter(3))
    face_skin = Image.new('L', SIZE)
    face_skin.putdata([255 if r > 160 and g >= .75*r and b >= .7*r else 0
                      for r,g,b,a in source.get_flattened_data()])
    face = intersection(face, face_skin.filter(ImageFilter.MaxFilter(5)))
    glyph_color = Image.new('L', SIZE)
    glyph_color.putdata([255 if b > 155 and (r > 140 or g > 140) and a > 100 else 0
                        for r,g,b,a in source.get_flattened_data()])
    def glyph(box: tuple[int,int,int,int]) -> Image.Image:
        return intersection(intersection(glyph_color,rectangle(box)).filter(ImageFilter.MaxFilter(3)),rectangle(box))
    phone_screen = polygon([(559,548),(610,553),(616,646),(569,649)])
    return [
        ("Phone_Eye_VL", "手机左眼", glyph((566,582,583,599))),
        ("Phone_Eye_VR", "手机右眼", glyph((593,584,609,600))),
        ("Phone_Mouth", "手机嘴形", glyph((578,600,602,619))),
        ("Phone_Cheek_VL", "手机左侧几何点", glyph((568,600,576,609))),
        ("Phone_Cheek_VR", "手机右侧几何点", glyph((603,601,612,611))),
        ("Phone_Screen", "手机屏幕底层", phone_screen),
        ("Phone_Frame", "手机机身与固定夹", polygon([(548,540),(613,546),(621,552),(629,649),(622,665),(559,663),(550,651),(540,549)])),
        ("Eye_VL_Brow", "画面左眉", polygon([(445,164),(456,161),(470,158),(482,158),(487,160),(486,162),(473,161),(459,163),(447,166)])),
        ("Eye_VR_Brow", "画面右眉", polygon([(520,159),(528,154),(540,151),(554,151),(565,154),(575,158),(574,160),(562,157),(553,154),(540,154),(529,157),(522,162)])),
        ("Eye_VL_UpperLash", "画面左上睫毛", lashes_vl),
        ("Eye_VR_UpperLash", "画面右上睫毛", lashes_vr),
        ("Eye_VL_LowerLash", "画面左下睫毛", polygon([(454,192),(461,196),(474,198),(487,194),(488,198),(477,202),(464,201),(456,197)])),
        ("Eye_VR_LowerLash", "画面右下睫毛", polygon([(530,185),(537,190),(547,191),(557,185),(562,184),(560,190),(549,196),(537,195),(530,190)])),
        ("Eye_VL_Iris", "画面左虹膜与瞳孔", intersection(ellipse((462,171,491,200)), eye_vl)),
        ("Eye_VR_Iris", "画面右虹膜与瞳孔", intersection(ellipse((527,164,555,195)), eye_vr)),
        ("Eye_VL_Sclera", "画面左眼白", eye_vl),
        ("Eye_VR_Sclera", "画面右眼白", eye_vr),
        ("Mouth_UpperLip", "上唇与闭嘴线", rectangle((499,220,536,225))),
        ("Mouth_LowerLip", "下唇", rectangle((499,225,536,230))),
        ("Face_Base", "脸部及耳朵底层", face),
        ("Hair_Bow", "大蝴蝶结", polygon([(306,179),(341,144),(351,123),(358,120),(332,98),(359,86),(370,40),(412,43),(429,9),(451,36),(490,16),(491,47),(484,62),(495,62),(485,77),(472,89),(459,109),(446,116),(411,144),(397,181),(386,219),(345,277),(354,229),(362,186),(336,203)])),
        ("Hair_PonytailClasp", "侧马尾蝶扣", polygon([(562,201),(584,210),(609,202),(622,199),(618,218),(612,226),(620,242),(603,240),(589,228),(580,240),(562,244),(561,229),(575,217)])),
        ("Neck_Skin", "脖颈连接", intersection(polygon([(480,233),(546,232),(548,264),(481,264)]),skin)),
        ("Hair_Side_VL", "画面左侧发", polygon([(423,139),(445,164),(442,185),(452,211),(467,226),(461,241),(471,271),(490,300),(473,304),(453,279),(438,257),(422,238),(407,249),(394,237),(386,214),(393,186),(409,169)])),
        ("Hair_Side_VR", "画面右侧发", polygon([(561,132),(579,148),(590,162),(589,185),(578,207),(558,230),(566,212),(573,192),(574,173)])),
        ("Hair_Bangs", "前刘海", polygon([(408,119),(428,92),(463,62),(501,61),(535,66),(549,106),(546,125),(531,147),(518,161),(508,172),(498,178),(489,172),(476,170),(464,176),(453,181),(440,193),(423,198),(412,181)])),
        ("Hair_Ponytail", "侧马尾", polygon([(577,216),(599,225),(625,243),(652,271),(670,309),(704,340),(729,393),(733,440),(754,452),(776,407),(760,357),(736,310),(716,275),(687,243),(643,216),(600,202)])),
        ("Hair_Back", "后发与头顶", rectangle((0,0,1024,290))),
        ("Hand_VL", "画面左手", intersection(polygon([(142,647),(175,663),(169,686),(151,709),(135,728),(123,736),(136,709),(110,749),(99,758),(90,757),(80,766),(63,769),(64,752),(97,708),(118,687)]),skin)),
        ("Hand_VR", "画面右手", intersection(polygon([(831,649),(858,663),(868,684),(903,717),(931,760),(928,773),(915,768),(904,756),(897,755),(880,734),(885,754),(875,749),(858,726),(848,706),(832,681)]),skin)),
        ("Sleeve_VL", "画面左袖与袖口", polygon([(293,444),(320,463),(343,497),(357,532),(316,554),(306,586),(294,616),(238,647),(221,663),(192,677),(168,659),(137,642),(146,623),(162,601),(153,572),(153,550),(187,527),(214,508),(245,475)])),
        ("Sleeve_VR", "画面右袖与袖口", polygon([(704,449),(744,468),(778,514),(818,541),(847,557),(849,590),(831,606),(844,625),(862,646),(833,675),(815,686),(789,652),(743,652),(699,626),(687,591),(661,557),(678,535),(687,502)])),
        ("Coat_VL", "画面左披肩裁片", polygon([(479,270),(401,286),(360,301),(338,327),(299,362),(232,416),(256,445),(223,474),(207,516),(239,534),(280,503),(300,518),(285,545),(325,527),(350,552),(383,566),(400,514),(429,518),(451,514),(454,477),(442,427),(422,376),(408,333),(449,294)])),
        ("Coat_VR", "画面右披肩裁片", polygon([(566,271),(631,285),(678,304),(695,334),(709,365),(772,410),(776,424),(739,447),(773,479),(800,520),(775,535),(734,493),(714,490),(700,525),(660,552),(625,569),(606,532),(621,499),(603,443),(618,395),(635,357),(660,319),(609,298)])),
        ("Belt", "腰带", polygon([(426,536),(548,539),(576,556),(570,579),(432,566),(397,578),(413,554)])),
        ("Torso_InnerTop", "内搭和高领", rectangle((0,250,1024,580))),
        ("Boot_VL", "画面左机械靴", rectangle((350,1274,515,1536))),
        ("Boot_VR", "画面右机械靴", rectangle((516,1278,676,1536))),
        ("ThighBand_VR", "白袜腿上部腿环", polygon([(515,853),(620,849),(624,877),(518,885)])),
        ("Sock_VR", "单层白色中筒袜", polygon([(545,1183),(569,1194),(600,1199),(625,1187),(622,1245),(616,1300),(553,1300),(548,1247)])),
        ("Sock_VL", "深色不透长袜", polygon([(386,827),(404,813),(445,810),(481,815),(501,831),(498,1000),(481,1052),(496,1160),(499,1300),(403,1300),(393,1181),(385,1121),(395,1052),(409,997),(400,879)])),
        ("Leg_VL_Skin", "深袜腿裸露部分", intersection(polygon([(380,749),(500,787),(500,832),(446,811),(385,831)]),skin)),
        ("Leg_VR_Skin", "白袜腿裸露部分", intersection(polygon([(512,802),(611,808),(621,842),(624,936),(603,1004),(608,1046),(628,1120),(628,1184),(593,1201),(546,1186),(539,1140),(532,1061),(523,1000),(517,943)]),skin)),
        ("Skirt_Front", "主裙与前网裙裁片", polygon([(425,552),(546,557),(586,568),(609,632),(658,651),(687,710),(709,754),(678,802),(628,843),(595,821),(537,803),(511,789),(460,782),(419,763),(385,757),(358,781),(313,807),(300,783),(327,746),(292,731),(316,682),(297,675),(314,633),(339,593),(377,576)])),
        ("Skirt_Side_VL", "画面左侧裙摆", rectangle((0,570,384,1274))),
        ("Skirt_Side_VR", "画面右侧裙摆", rectangle((629,570,1024,1278))),
        ("Skirt_Base", "主裙残余与内层", rectangle((0,570,1024,852))),
        ("Leg_VL_Outline", "深袜腿轮廓连接", rectangle((384,852,515,1274))),
        ("Leg_VR_Outline", "白袜腿轮廓连接", rectangle((515,852,629,1278))),
    ]


def repair_behind_features(parts: dict[str, Image.Image], source: Image.Image, face_patch: Path) -> dict[str,list[str]]:
    notes: dict[str,list[str]] = {}
    original_patch = Image.open(face_patch).convert("RGBA")
    bbox = original_patch.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("AI face repair is empty")
    head = original_patch.crop(bbox).resize((175,149), Image.Resampling.LANCZOS)
    under = Image.new("RGBA", SIZE)
    under.alpha_composite(head, (428,103))
    # Use the clean central head; generated ears do not align with the visible source ear.
    clean_head = polygon([(446,147),(450,126),(472,110),(505,103),(536,107),
                          (559,121),(577,146),(580,170),(579,192),(569,217),
                          (551,238),(529,248),(512,252),(493,241),(474,231),
                          (456,214),(445,194)])
    under.putalpha(ImageChops.multiply(under.getchannel('A'),clean_head))
    # Restrict repairs to the approved silhouette; no background or silhouette redesign.
    support = source.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    under.putalpha(ImageChops.multiply(under.getchannel("A"),support))
    under.putalpha(under.getchannel("A").point(lambda value: 255 if value >= 240 else value))
    under.alpha_composite(parts["Face_Base"])
    parts["Face_Base"] = under
    notes["Face_Base"] = ["AI bald-face repair resized/aligned beneath source face; original visible face pixels retained", "Hidden forehead/scalp and feature sockets supplied; only small-angle modeling intended"]
    for key, box, color in [("Eye_VL_Sclera",(450,174,497,201),(250,239,247,255)),("Eye_VR_Sclera",(523,167,567,195),(250,239,247,255))]:
        base = Image.new("RGBA",SIZE)
        ImageDraw.Draw(base).ellipse(box,fill=color)
        # Only the observed aperture is active; hidden sclera extends beneath the iris.
        feature_masks = [parts[key].getchannel("A")]
        for suffix in ("Iris","UpperLash","LowerLash"):
            feature_masks.append(parts[key.replace("Sclera",suffix)].getchannel("A"))
        aperture = union(*feature_masks).point(lambda value:255 if value else 0)
        base.putalpha(ImageChops.multiply(base.getchannel("A"),aperture))
        base.alpha_composite(parts[key])
        parts[key] = base
        notes[key] = ["Opaque matching sclera supplied underneath original iris; no iris repaint"]
    phone = parts["Phone_Screen"]
    filled = Image.new("RGBA",SIZE)
    glyph_mask = union(*[parts[key].getchannel("A") for key in ("Phone_Eye_VL","Phone_Eye_VR","Phone_Mouth","Phone_Cheek_VL","Phone_Cheek_VR")])
    glyph_mask = intersection(glyph_mask,polygon([(559,548),(610,553),(616,646),(569,649)]))
    pixels=filled.load()
    for y in range(548,650):
        for x in range(559,617):
            if glyph_mask.getpixel((x,y)):
                t=(y-548)/102
                pixels[x,y]=(int(40+5*t),int(45+2*t),int(86+10*t),255)
    filled.alpha_composite(phone)
    parts["Phone_Screen"]=filled
    notes["Phone_Screen"]=["Screen background reconstructed under the geometric face for future expression changes"]
    return notes


def psd_bytes(layers: list[tuple[str,Image.Image]], merged: Image.Image) -> bytes:
    """Adobe PSD v1, RGB/8bit, cropped planar raw channels, embedded sRGB."""
    records=[];payload=[]
    for name,image in reversed(layers):  # PSD records are front to back.
        bbox=image.getchannel("A").getbbox()
        if bbox is None:
            raise ValueError(f"Empty layer: {name}")
        x0,y0,x1,y1=bbox;crop=image.crop(bbox)
        channels=[(0,crop.getchannel('R')),(1,crop.getchannel('G')),(2,crop.getchannel('B')),(-1,crop.getchannel('A'))]
        channel_data=[struct.pack('>H',0)+channel.tobytes() for _,channel in channels]
        ascii_name=name.encode('ascii')
        if len(ascii_name)>255:
            raise ValueError('PSD layer name exceeds Pascal length')
        pascal=bytes([len(ascii_name)])+ascii_name
        pascal+=b'\0'*((-len(pascal))%4)
        unicode_data=struct.pack('>I',len(name))+name.encode('utf-16-be')
        unicode_block=b'8BIMluni'+struct.pack('>I',len(unicode_data))+unicode_data
        unicode_block+=b'\0'*(len(unicode_data)%2)
        extra=struct.pack('>II',0,0)+pascal+unicode_block
        record=struct.pack('>iiiiH',y0,x0,y1,x1,4)
        record+=b''.join(struct.pack('>hI',cid,len(data)) for (cid,_),data in zip(channels,channel_data))
        record+=b'8BIMnorm'+bytes([255,0,0,0])+struct.pack('>I',len(extra))+extra
        records.append(record);payload.extend(channel_data)
    layer_info=struct.pack('>h',-len(layers))+b''.join(records)+b''.join(payload)
    layer_info+=b'\0'*(len(layer_info)%2)
    layer_section=struct.pack('>I',len(layer_info))+layer_info+struct.pack('>I',0)
    icc=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes()
    resources=b'8BIM'+struct.pack('>H',1039)+b'\0\0'+struct.pack('>I',len(icc))+icc+b'\0'*(len(icc)%2)
    header=b'8BPS'+struct.pack('>H',1)+b'\0'*6+struct.pack('>HIIHH',4,SIZE[1],SIZE[0],8,3)
    merged_data=struct.pack('>H',0)+b''.join(merged.getchannel(c).tobytes() for c in 'RGBA')
    return header+struct.pack('>I',0)+struct.pack('>I',len(resources))+resources+struct.pack('>I',len(layer_section))+layer_section+merged_data


def composite(layers: list[tuple[str,Image.Image]]) -> Image.Image:
    result=Image.new('RGBA',SIZE)
    for _,image in layers:
        result.alpha_composite(image)
    return result


def white_preview(image:Image.Image)->Image.Image:
    result=Image.new('RGBA',SIZE,'white');result.alpha_composite(image)
    return result.convert('RGB')


def main() -> None:
    parser=argparse.ArgumentParser()
    parser.add_argument('--face-repair',type=Path,required=True)
    parser.add_argument('--output',type=Path,default=ART/'Separation-v005')
    args=parser.parse_args()
    output=args.output.resolve()
    if output.exists():
        raise FileExistsError(f'Refusing to overwrite existing output: {output}')
    source=Image.open(SOURCE).convert('RGBA')
    if source.size!=SIZE:
        raise ValueError('Approved source canvas changed')
    source_hash=sha(SOURCE)
    remaining=source.getchannel('A').point(lambda value:255 if value else 0)
    raw_parts={};labels={};ownership={}
    specs=anatomical_specs(source)
    for key,label,mask in specs:
        if key == 'Hand_VL':
            mask=union(mask.filter(ImageFilter.MaxFilter(7)),rectangle((0,675,180,820)))
        elif key == 'Hand_VR':
            mask=union(mask.filter(ImageFilter.MaxFilter(7)),rectangle((820,695,1024,820)))
        owned=intersection(mask,remaining)
        remaining=ImageChops.subtract(remaining,owned)
        image=cut(source,owned)
        if image.getchannel('A').getbbox() is None:
            raise ValueError(f'Empty anatomical part: {key}')
        raw_parts[key]=image;labels[key]=label;ownership[key]=owned
    if remaining.getbbox() is not None:
        # Edges beyond hand polygons belong to their adjacent limbs, not a whole-image fallback.
        for key,box in [('Hand_VL',(0,580,200,820)),('Hand_VR',(800,580,1024,820)),
                        ('Skirt_Side_VL',(0,580,384,1280)),('Skirt_Side_VR',(628,580,1024,1280)),
                        ('Boot_VL',(0,1274,515,1536)),('Boot_VR',(515,1278,1024,1536))]:
            owned=intersection(remaining,rectangle(box))
            if owned.getbbox():
                ownership[key]=union(ownership[key],owned)
                raw_parts[key]=cut(source,ownership[key])
                remaining=ImageChops.subtract(remaining,owned)
    if remaining.getbbox() is not None:
        raise ValueError(f'Unassigned approved pixels: {remaining.getbbox()}')
    # The highlighted cheek strands and vertical forehead strand have skin-like colors.
    strands = {
        'Hair_Side_VR': union(polygon([(548,121),(551,126),(555,144),(559,158),(567,168),
                                      (564,170),(556,161),(552,147)]),
                             polygon([(576,181),(574,194),(569,208),(563,219),(558,224),
                                      (564,211),(568,202),(572,185)])),
        'Hair_Side_VL': polygon([(449,193),(451,202),(455,209),(462,217),(462,220),
                                 (453,213),(448,203),(446,193)]),
        'Hair_Bangs': polygon([(496,175),(510,172),(504,180),(495,184),(487,191),(485,188)]),
    }
    for hair_key,hair_mask in strands.items():
        moved=intersection(ownership['Face_Base'],hair_mask)
        ownership['Face_Base']=ImageChops.subtract(ownership['Face_Base'],moved)
        ownership[hair_key]=union(ownership[hair_key],moved)
        raw_parts[hair_key]=cut(source,ownership[hair_key])
    raw_parts['Face_Base']=cut(source,ownership['Face_Base'])
    # Keep residual seams with the actual anatomical layer, rather than isolated slivers.
    for seam, parent in [('Leg_VL_Outline','Sock_VL'),('Leg_VR_Outline','Leg_VR_Skin'),('Skirt_Base','Skirt_Front')]:
        ownership[parent]=union(ownership[parent],ownership.pop(seam))
        raw_parts[parent]=cut(source,ownership[parent])
        del raw_parts[seam]
        del labels[seam]
    # The lossless partition is independently verifiable before repairs.
    raw_order=list(reversed(list(raw_parts.items())))
    raw_merged=composite(raw_order)
    alpha_difference=ImageChops.difference(raw_merged.getchannel('A'),source.getchannel('A')).getbbox()
    visible_rgb_difference=ImageChops.difference(white_preview(raw_merged),white_preview(source)).getbbox()
    if alpha_difference or visible_rgb_difference:
        raise ValueError('Source partition does not reconstruct the approved source exactly')
    parts={key:cut(source,mask,True) for key,mask in ownership.items()}
    repair_notes=repair_behind_features(parts,source,args.face_repair)
    order=list(reversed(list(parts.items())))
    # The scalp repair must sit behind every source hair layer.
    face_item=next(item for item in order if item[0]=='Face_Base')
    order.remove(face_item)
    hair_index=next(i for i,item in enumerate(order) if item[0]=='Hair_Back')
    order.insert(hair_index,face_item)
    merged=composite(order)
    model_preview=white_preview(merged);reference_preview=white_preview(source)
    diff=ImageChops.difference(model_preview,reference_preview)
    histogram=diff.histogram();pixel_count=SIZE[0]*SIZE[1]
    mean_absolute_rgb_difference=sum((i%256)*count for i,count in enumerate(histogram))/(pixel_count*3)
    output.mkdir(parents=True)
    layers_dir=output/'Layers';layers_dir.mkdir()
    manifest={'schemaVersion':1,'status':'artwork-ready-for-import-check','source':SOURCE.relative_to(ROOT).as_posix(),
              'sourceSha256':source_hash,'canvas':list(SIZE),'coordinateConvention':'VL/VR mean viewer left/right, not anatomical left/right',
              'method':'explicitly authorized script separation; original-pixel partition plus recorded hidden repairs',
              'psd':'karolina-live2d-materials.psd','drawOrder':'layers listed from back to front','layers':[],
              'faceRepair':{'inputSha256':sha(args.face_repair),'inputAlphaBounds':list(Image.open(args.face_repair).getchannel('A').getbbox()),
                            'alignedSize':[175,149],'alignedPosition':[428,103],'generatedEarsExcluded':True},
              'verification':{'losslessSourcePartitionAlphaExact':True,'losslessSourcePartitionWhiteCompositeExact':True,
                              'repairedCompositeMeanAbsoluteRgbDifference':mean_absolute_rgb_difference,'cubismImport':'pending'},
              'limitations':['Not a rig: no parameter bindings, meshes adjusted for motion, physics, moc3 or model3.json.',
                             'Approved master is 1024x1536; eye source details are small.',
                             'Garment layers and side skirt panels are coarse first-pass cuts; large arm/body turns need more hidden repair.',
                             'Iris completion outside the observed eye aperture is not supplied; wide gaze requires additional paint.',
                             'No software screenshots used as verification.']}
    for index,(key,image) in enumerate(order):
        path=layers_dir/f'{index:02d}_{key}.png';image.save(path)
        manifest['layers'].append({'id':key,'label':labels[key],'path':path.relative_to(output).as_posix(),
                                  'sha256':sha(path),'alphaBounds':list(image.getchannel('A').getbbox()),
                                  'sourcePixels':sum(ownership[key].histogram()[255:]),'repairs':repair_notes.get(key,[])})
    psd=output/manifest['psd'];psd.write_bytes(psd_bytes(order,merged));manifest['psdSha256']=sha(psd)
    (output/'karolina-source-partition.psd').write_bytes(psd_bytes(raw_order,raw_merged))
    model_preview.save(output/'preview-on-white.png')
    merged.save(output/'preview-transparent.png')
    reference_preview.save(output/'approved-reference-on-white.png')
    diff.save(output/'repaired-difference.png')
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',11)
    cols=6;cell=(170,180);rows=(len(order)+cols-1)//cols
    sheet=Image.new('RGB',(cols*cell[0],rows*cell[1]),(228,229,236));draw=ImageDraw.Draw(sheet)
    for index,(key,image) in enumerate(order):
        x=(index%cols)*cell[0];y=(index//cols)*cell[1]
        cropped=image.crop(image.getchannel('A').getbbox());cropped.thumbnail((154,150),Image.Resampling.LANCZOS)
        sheet.paste(cropped,(x+(170-cropped.width)//2,y+(153-cropped.height)//2),cropped)
        draw.text((x+4,y+158),key,font=font,fill=(30,30,45))
    sheet.save(output/'parts-contact-sheet.png')
    with (output/'layers.json').open('x',encoding='utf-8') as stream:
        json.dump(manifest,stream,ensure_ascii=False,indent=2);stream.write('\n')
    if sha(SOURCE)!=source_hash:
        raise RuntimeError('Approved source changed during separation')
    print(json.dumps({'output':str(output),'layers':len(order),'psdBytes':psd.stat().st_size,
                      'losslessPartitionExact':True,'repairedMeanAbsoluteRgbDifference':mean_absolute_rgb_difference},ensure_ascii=False))


if __name__=='__main__':
    main()
