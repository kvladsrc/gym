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
using std::gcd;
using std::map;
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

    int best = n;
    int best_sum = n;
    for (int i = 2; i <= n; ++i) {
      int local_sum = 0;
      for (int j = 1; i * j <= n; ++j) {
        local_sum += i * j;
      }
      if (best_sum < local_sum) {
        best_sum = local_sum;
        best = i;
      }
    }

    std::cout << best << "\n";
  }

  return 0;
}
