from gamex import family

# get family
family = family.getFamily('Bioware')
print(f'studio: {family.studio}')

file = ('game:/base.zip#MDK2', 'sample:*')
# file = ('game:/#SWTOR', 'sample:*')

# get pak with game:/uri
pakFile = family.openPakFile(file[0])
sample = pakFile.game.getSample(file[1][7:]).path if file[1].startswith('sample') else file[1]
print(f'pak: {pakFile}, {sample}')

# get file
data = pakFile.loadFileData(sample)
print(f'dat: {data}')