#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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
    int64_t x = 0;
    int64_t n = 0;
    std::cin >> x >> n;

    int64_t d = 0;
    switch (n % 4) {
      case 1:
        d = n;
        break;
      case 2:
        d = -1;
        break;
      case 3:
        d = -n - 1;
        break;
      default:
        d = 0;
    }

    if ((x % 2) == 0) {
      std::cout << x - d << "\n";
    } else {
      std::cout << x + d << "\n";
    }
  }

  return 0;
}
