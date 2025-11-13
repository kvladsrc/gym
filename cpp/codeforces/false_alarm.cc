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
    int x = 0;
    std::cin >> n >> x;

    vector<int> a(n);
    int first_closed = -1;
    int last_closed = -1;

    for (int i = 1; i <= n; ++i) {
      int buf = 0;
      std::cin >> buf;
      if (buf == 1) {
        last_closed = i;
        if (first_closed == -1) {
          first_closed = i;
        }
      }
    }

    if (first_closed == -1) {
      std::cout << "YES" << "\n";
    } else {
      std::cout << (last_closed - first_closed + 1 <= x ? "YES" : "NO") << "\n";
    }
  }

  return 0;
}
