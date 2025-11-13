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
    int n;
    std::cin >> n;
    int64_t prev = 0;
    map<int64_t, bool> diffs_map;
    diffs_map[0] = true;
    bool res = false;
    for (int i = 1; i <= n; ++i) {
      int64_t buf;
      std::cin >> buf;
      if (i % 2) {
        buf = -buf;
      }
      prev += buf;
      if (diffs_map[prev]) {
        res = true;
      }

      diffs_map[prev] = true;
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
