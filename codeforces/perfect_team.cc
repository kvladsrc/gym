#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
    int c, m, x;
    std::cin >> c >> m >> x;

    int res = 0;
    // Start greedy with teams {c, m, x}.
    res += min(x, min(c, m));
    m -= res;
    c -= res;
    x -= res;

    while (c && m) {
      if (c == m) {
        res += (c / 3) * 2;
        if ((c % 3) == 2)
          res++;
        break;
      }

      auto delta = min(max(c, m) - min(c, m), min(c, m));
      if (c > m) {
        c -= delta * 2;
        m -= delta;
      } else {
        m -= delta * 2;
        c -= delta;
      }
      res += delta;
    }

    std::cout << res << "\n";
  }

  return 0;
}
