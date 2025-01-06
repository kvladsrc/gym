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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    vector<int> a(n);
    int64_t total = 0;
    for (auto& i : a) {
      std::cin >> i;
      total += i;
    }

    int res = 0;
    for (int count = n; count >= 1; --count) {
      if ((total % count) != 0) {
        continue;
      }

      auto val = total / count;
      auto a_cpy = a;

      int buf = 0;
      while (!a_cpy.empty()) {
        if (buf == val) {
          buf = 0;
          continue;
        }

        if (buf > val) {
          break;
        }

        buf += a_cpy.back();
        a_cpy.pop_back();
      }

      if (buf == val) {
        res = n - count;
        break;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
