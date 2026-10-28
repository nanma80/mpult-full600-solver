"""Permutation parity and deterministic three-cycle decomposition."""

def permutation_parity(p):
    seen=set();sign=0
    for start in range(len(p)):
        if start in seen:continue
        q=start;length=0
        while q not in seen:seen.add(q);length+=1;q=p[q]
        if q!=start:raise ValueError('Not a permutation')
        sign^=(length-1)&1
    return sign

def apply_cycle(values,cycle):
    a,b,c=cycle
    values[a],values[b],values[c]=values[c],values[a],values[b]

def three_cycles(permutation):
    if sorted(permutation)!=list(range(len(permutation))):raise ValueError('Not a permutation')
    swaps=[];seen=set()
    for start in range(len(permutation)):
        if start in seen:continue
        cycle=[];q=start
        while q not in seen:seen.add(q);cycle.append(q);q=permutation[q]
        swaps.extend((cycle[0],p) for p in cycle[1:])
    if len(swaps)%2:raise ValueError('Odd permutation has no three-cycle decomposition')
    result=[]
    for first,second in zip(swaps[::2],swaps[1::2]):
        common=set(first)&set(second)
        if len(common)==2:continue
        if common:
            def image(x):
                for a,b in (first,second):
                    if x==a:x=b
                    elif x==b:x=a
                return x
            a=min(set(first)|set(second));b=image(a);c=image(b)
            assert image(c)==a
            result.append((a,b,c))
        else:
            a,b=first;c,d=second
            result.extend([(a,c,d),(a,c,b)])
    values=permutation.copy()
    for cycle in result:apply_cycle(values,cycle)
    assert values==list(range(len(values)))
    assert len(result)<=len(permutation)
    return result

