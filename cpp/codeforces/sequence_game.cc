#include <algorithm>
#include <cstddef>
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
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (int i = 0; i < n; i++) {
      std::cin >> a[i];
    }

    vector<int> guess;
    guess.push_back(a[0]);
    for (size_t i = 1; i < a.size(); i++) {
      if (a[i] >= a[i - 1]) {
        guess.push_back(a[i]);
      } else {
        guess.push_back(a[i]);
        guess.push_back(a[i]);
      }
    }

    std::cout << guess.size() << std::endl;
    for (int i : guess) {
      std::cout << i << " ";
    }
    std::cout << std::endl;
  }

  return 0;
}
