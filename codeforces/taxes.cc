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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

bool is_prime(int n) {
  if (n <= 1) {
    return false;
  }

  for (int d = 2; d * d <= n; ++d) {
    if (n % d == 0) {
      return false;
    }
  }

  return true;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  /*

    If number is prime we pay least possible value -- 1 burle. Lets
   try greedy: cost(n) = 1 + cost(n - largest_prime(n)). Since n <=
   2*10^9 Erato table will require 8 GiB, so not an option.

   Greedy does not works.

   Goldbach's conjecture: every even natural number greater than 2 is
   the sum of two prime numbers. It seems at least, that every integer
   greater than 2 can be written as the sum of three primes.

  */

  if (n == 2) {
    std::cout << 1 << "\n";
    return 0;
  }

  if (n % 2 == 0) {
    std::cout << 2 << "\n";
    return 0;
  }

  if (is_prime(n)) {
    std::cout << 1 << "\n";
    return 0;
  }

  if (is_prime(n - 2)) {
    std::cout << 2 << "\n";
    return 0;
  }

  std::cout << 3 << "\n";

  return 0;
}
