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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b;
    std::cin >> a >> b;
    int res;

    if (a == b) {
      res = (a / 3) * 2;
      if ((a % 3 + b % 3) >= 3) res++;
      std::cout << res << "\n";
      continue;
    }

    int min_resourses = min(a, b);
    int max_resources = max(a, b);
    int delta = max_resources - min_resourses;
    if (delta >= min_resourses) {
      res = min_resourses;
    } else {
      res = ((min_resourses - delta) / 3) * 2 + delta;
      if ((min_resourses - delta) % 3 == 2) res++;
    }

    std::cout << res << "\n";
  }

  return 0;
}
