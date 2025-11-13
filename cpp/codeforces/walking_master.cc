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
    int a = 0;
    int b = 0;
    int c = 0;
    int d = 0;

    std::cin >> a >> b >> c >> d;

    if (d < b) {
      std::cout << -1 << "\n";
      continue;
    }

    if ((c - a) > (d - b)) {
      std::cout << -1 << "\n";
      continue;
    }

    std::cout << 2 * (d - b) + a - c << "\n";
  }

  return 0;
}
