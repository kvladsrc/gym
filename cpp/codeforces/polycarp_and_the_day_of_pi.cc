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

  string pi = "314159265358979323846264338327";
  while (t--) {
    string in;
    std::cin >> in;

    int res = 0;
    for (size_t i = 0; i < in.size(); ++i) {
      if (in[i] == pi[i]) {
        res++;
      } else {
        break;
      }
    }
    std::cout << res << "\n";
  }
  return 0;
}
