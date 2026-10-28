"""MPUlt log parser. History is exposed for checksum verification only."""
def read_log(path):
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    end = lines.index('EndPuzzle')
    width, count = map(int, lines[end+1].split())
    encoded = ''
    i = end+2
    while len(encoded) < width*count:
        encoded += lines[i]
        i += 1
    assert len(encoded) == width*count
    alphabet = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
    colors = [sum(alphabet.index(c)*36**j for j,c in enumerate(encoded[k:k+width]))
              for k in range(0,len(encoded),width)]
    return dict(header=lines[0], description=lines[1:end+1], width=width,
                colors=colors, metadata=lines[i:lines.index('*')],
                history=' '.join(lines[lines.index('*')+1:]))
