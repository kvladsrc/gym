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

  int t;
  std::cin >> t;
  while ((t--) != 0) {
    int n;
    std::cin >> n;
    vector<int64_t> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    int64_t maximal = abs(a.front());
    int64_t minimal = a.front();
    for (int i = 1; i < n; ++i) {
      maximal = max(abs(maximal + a[i]), abs(minimal + a[i]));
      minimal = minimal + a[i];
    }

    std::cout << maximal << "\n";
  }

  return 0;
}
