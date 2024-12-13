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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int64_t n;
    std::cin >> n;

    bool res = false;
    for (int k = 2; 1 + k + k * k <= n; ++k) {
      auto local = n - 1;
      while (local && local % k == 0) {
        local /= k;
        local--;
      }

      if (!local) {
        res = true;
        break;
      }
    }
    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
