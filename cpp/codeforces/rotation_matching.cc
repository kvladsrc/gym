#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  for (int i = 1; i <= n; ++i) {
    int buf;
    std::cin >> buf;
    a[buf - 1] = i;
  }

  unordered_map<int, int> dists;
  int best = 0;
  for (int i = 1; i <= n; ++i) {
    int buf;
    std::cin >> buf;
    int local_dist = a[buf - 1] - i;
    if (local_dist < 0) {
      local_dist = n - i + a[buf - 1];
    }
    dists[local_dist]++;
    best = max(dists[local_dist], best);
  }

  std::cout << best << "\n";

  return 0;
}
