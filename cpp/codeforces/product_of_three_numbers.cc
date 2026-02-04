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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    // Searching for three distinct dividers of n. 2 <= a, b, c, such
    // that a * b * c = n.
    vector<int> dividers;
    int rem = n;
    for (int i = 2; i * i < rem; ++i) {
      if (rem % i == 0) {
        dividers.push_back(i);
        rem /= i;
      }
    }
    dividers.push_back(rem);

    if (dividers.size() < 3) {
      std::cout << "NO\n";
    } else {
      std::cout << "YES\n";
      std::cout << dividers[0] << " " << dividers[1] << " "
                << n / (dividers[0] * dividers[1]) << "\n";
    }
  }

  return 0;
}
