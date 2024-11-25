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
    if (n % 2 == 0) {
      // a = b, a + b = n, LCM(a, b) = a = b
      std::cout << n / 2 << " ";
      std::cout << n / 2 << "\n";
      continue;
    }

    int first = 1, second = n - 1;
    for (int guess = 2; guess * guess <= n; ++guess) {
      if (n % guess == 0) {
        first = n / guess;
        second = n - first;
        break;
      }
    }

    std::cout << first << " ";
    std::cout << second << "\n";
  }

  return 0;
}
