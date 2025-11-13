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
    int n = 0;
    std::cin >> n;
    vector<int> a(8);

    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;

      for (int p = 0; p < 8; ++p) {
        a[p] += buf % 2;
        buf /= 2;
      }
    }

    int x = 0;
    for (int i = 0; i < 8; ++i) {
      if ((a[i] % 2) == 0) {
        continue;
      }

      if ((n - a[i]) % 2 == 0) {
        x += 1 << i;
        continue;
      }

      x = -1;
      break;
    }

    std::cout << x << "\n";
  }

  return 0;
}
