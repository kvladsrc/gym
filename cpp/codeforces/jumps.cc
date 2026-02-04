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

int sum_seq(int k) { return ((1 + k) * k) / 2; }

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int x;
    std::cin >> x;

    int k = 1;
    while (sum_seq(k) < x) {
      k++;
    }

    int diff = sum_seq(k) - x;
    if (diff == 1) {
      std::cout << k + 1 << "\n";
    } else {
      std::cout << k << "\n";
    }
  }

  return 0;
}
