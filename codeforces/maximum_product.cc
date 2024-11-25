#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <iterator>
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
    int n;
    std::cin >> n;

    vector<int64_t> all(n);
    for (auto &i : all) {
      std::cin >> i;
    }
    sort(all.begin(), all.end());

    int64_t res = 1;
    // We can took 0, 2 or 4 negative numbers.
    int64_t case1 = INT64_MIN, case2 = INT64_MIN, case3 = INT64_MIN;
    if (all.size() >= 2 && all.size() >= 3) {
      case1 = all[0];
      case1 *= all[1];
      case1 *= all[all.size() - 1];
      case1 *= all[all.size() - 2];
      case1 *= all[all.size() - 3];
    }

    if (all.size() >= 4 && all.size() >= 1) {
      case2 = all[0];
      case2 *= all[1];
      case2 *= all[2];
      case2 *= all[3];
      case2 *= all[all.size() - 1];
    }

    if (all.size() >= 5) {
      case3 = all[all.size() - 1];
      case3 *= all[all.size() - 2];
      case3 *= all[all.size() - 3];
      case3 *= all[all.size() - 4];
      case3 *= all[all.size() - 5];
    }

    res = max(case1, max(case2, case3));
    std::cout << res << "\n";
  }

  return 0;
}
