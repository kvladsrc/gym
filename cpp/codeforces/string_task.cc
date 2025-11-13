#include <algorithm>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

bool is_vowel(char c) {
  vector<char> vowels = {'a', 'o', 'y', 'e', 'u', 'i'};
  c = std::tolower(c);
  return std::any_of(vowels.begin(), vowels.end(),
                     [c](char v) { return c == v; });
}

// O(n)
string task(const string &input) {
  string res;
  for (char c : input) {
    if (is_vowel(c)) continue;

    res += '.';
    res += std::tolower(c);
  }

  return res;
}

int main(int argc, char *argv[]) {
  string input;
  std::cin >> input;
  std::cout << task(input) << "\n";
  return 0;
}
