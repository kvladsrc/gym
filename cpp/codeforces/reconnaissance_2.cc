#include <algorithm>
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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int best_first = 0;
  int best_second = 1;

  for (int i = 0; i < n; ++i) {
    if ((abs(a[i] - a[(i + 1) % n])) < (abs(a[best_first] - a[best_second]))) {
      best_first = i;
      best_second = (i + 1) % n;
    }
  }

  std::cout << best_first + 1 << " ";
  std::cout << best_second + 1 << "\n";

  return 0;
}
