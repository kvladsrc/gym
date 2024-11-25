#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    // n = k
    // b..a
    // .ba.
    // .ab.
    // a..b
    // b..a
    // ....
    // They never pretend to be on one spot.
    // b...a
    // .b.a.
    // ..ab. <- collision k = 3
    // .a..b
    // ab... <- collision k = 5
    // ..b.a
    // ...ab <- collision k = 7
    //
    // Posigion of B cat at k = (k + number of collision % n) if 0 ->
    // n. First collission occurs on (n / 2) + 1, then each n / 2
    // move.

    int res = k % n;
    if (!res)
      res = n;

    if (!(n % 2 == 0)) {
      int collisson_cnt = (k - 1) / (n / 2);
      res += collisson_cnt;
      res %= n;
      if (!res)
        res = n;
    }

    std::cout << res << "\n";
  }

  return 0;
}
