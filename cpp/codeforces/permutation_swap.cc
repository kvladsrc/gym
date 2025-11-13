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

int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    map<int, int> m;
    for (int i = 1; i <= n; ++i) {
      int buf = 0;
      std::cin >> buf;
      m[buf] = i;
    }

    int res = 0;
    for (int i = 1; i <= n; ++i) {
      res = gcd(res, m[i] - i);
    }

    std::cout << res << "\n";
  }

  return 0;
}
