from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parents[3]
folder=root/'ArtPreviews/IndustrialModules'
assert (folder/'Validation.txt').exists(),'Unity validation has not completed.'
models=[('SolarArray','SOLAR ARRAY / tracking wings'),('BatteryBank','BATTERY BANK / live charge'),('FuelCellGenerator','FUEL-CELL GENERATOR'),
 ('FuelTank','FUEL TANK / pressure vessel'),('IceRefinery','ICE REFINERY / cracking column'),('PilotLounge','PILOT LOUNGE / crew services'),
 ('CommsArray','COMMS ARRAY / scanning dish'),('AtcTower','TRAFFIC CONTROL / rotating radar'),('Hangar','HANGAR / open repair bay')]
font=lambda size:ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',size)
im=Image.new('RGB',(1800,2190),(12,19,29));d=ImageDraw.Draw(im)
d.text((40,25),'WAYSTATION / INDUSTRIAL MODULE KIT',font=font(40),fill=(229,231,214))
d.text((42,88),'Textured URP art  /  Working animation and FX slots  /  Actual Unity renders',font=font(23),fill=(122,161,178))
for i,(name,label) in enumerate(models):
 x=(i%3)*600;y=145+(i//3)*660
 with Image.open(folder/(name+'.png'))as art:
  art=art.convert('RGB').resize((570,570),Image.Resampling.LANCZOS);im.paste(art,(x+15,y))
 d.text((x+25,y+580),label,font=font(23),fill=(222,225,208))
d.text((40,2140),'Shared worn-metal palette  /  Emission maps  /  Foundation footprints preserved  /  Power and daylight aware',font=font(21),fill=(122,161,178))
im.save(root/'ArtPreviews/IndustrialModules.png')
print(root/'ArtPreviews/IndustrialModules.png')
