"""Compare predicted and app-saved colors and verify both log checksums."""
import argparse,json
from mpult_workflow import check
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('expected');ap.add_argument('actual');args=ap.parse_args()
    result=check(args.expected,args.actual);print(json.dumps(result,indent=2));raise SystemExit(0 if result['match'] else 1)
