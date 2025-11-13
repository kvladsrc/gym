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
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> exp(n);
    for (auto &i : exp) {
      std::cin >> i;
    }
    sort(exp.begin(), exp.end());
    int res = 0;
    int bucket = 0;
    for (auto i : exp) {
      if (i == 1) {
        res++;
        continue;
      }

      if (i <= bucket + 1) {
        bucket = 0;
        res++;
      } else {
        bucket++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
