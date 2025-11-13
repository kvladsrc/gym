#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> t(n);
  for (auto &i : t) {
    std::cin >> i;
  }
  sort(t.begin(), t.end());
  int time = 0;
  int res = 0;
  for (auto i : t) {
    if (i >= time) {
      time += i;
      res++;
    }
  }
  std::cout << res << "\n";

  return 0;
}
