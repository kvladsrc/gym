#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int min_moves(int n) {
  if (n == 1) return 2;
  if (n == 2) return 1;
  if (n == 3) return 1;
  if (n == 4) return 2;

  return n / 3 + (n % 3 ? 1 : 0);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    std::cout << min_moves(n) << "\n";
  }

  return 0;
}
