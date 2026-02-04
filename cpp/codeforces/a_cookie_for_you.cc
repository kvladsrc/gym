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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int64_t a, b, n, m;
    std::cin >> a >> b >> n >> m;
    // a -- vanila, b -- chocolate.
    //
    // n -- first type: if vanila_cnt > chocolate_cnt ? selects
    // vanila. Eats maximal cookie.
    //
    // m -- secont type: if vanila_cnt > chocolate_cnt ?  selects
    // chocolate. Eats minimal cookie.

    auto minimal_cnt = min(a, b);
    bool res = true;
    if (minimal_cnt < m) {
      res = false;
    }

    if (((a + b) - m) < n) {
      res = false;
    }

    std::cout << (res ? "Yes" : "No") << "\n";
  }

  return 0;
}
