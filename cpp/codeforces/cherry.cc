#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int64_t> a(n);
    for (auto& i : a) std::cin >> i;

    int64_t best = 0;
    for (int i = 0; i < n; ++i) {
      if (i > 0) best = max(best, a[i] * a[i - 1]);

      if (i < (n - 1)) best = max(best, a[i] * a[i + 1]);
    }

    std::cout << best << "\n";
  }

  return 0;
}
