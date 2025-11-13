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
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    int next = 1;
    if (n % 2) {
      std::cout << "3 1 2 ";
      n -= 3;
      next = 4;
    }

    while (n) {
      std::cout << next + 1 << " ";
      std::cout << next << " ";
      next += 2;
      n -= 2;
    }

    std::cout << "\n";
  }

  return 0;
}
