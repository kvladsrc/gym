#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

// Definition: l(X) -- maximal string which is prefix and suffix of
// string X not equal to X.
//
// Lemmas:
//
// - l(X), l(l(X)), l(l(l(X))), ... -- prefixies and suffixes of X. By
//   definition.
//
// - l(l(...(l(X)))) -- ends by empty string. Every string is shorter
//   than previous.
//
// - Every prefix&suffix of X exists in set l(X), l(l(X)),
//   l(...(l(X))). The opposite contradicts the definition.
vector<size_t> patt_prefs(const string &patt) {
  vector<size_t> l(patt.size());
  l.front() = 0;

  for (size_t idx = 1; idx < patt.size(); ++idx) {
    auto len = l[idx - 1];
    while (patt[len] != patt[idx] && (len > 0)) {
      len = l[len];
    }

    if (patt[len] == patt[idx]) {
      l[idx] = len + 1;
    } else {
      l[idx] = 0;
    }
  }

  return l;
}

bool kmp(const string &s, const string &patt) {
  auto pref = patt_prefs(patt);
  size_t len = 0;

  for (size_t idx = 0; idx < s.size(); ++idx) {
    while (patt[len] != s[idx] && (len > 0)) {
      len = pref[len];
    }

    if (patt[len] == s[idx]) {
      len++;
    } else {
      len = 0;
    }

    if (len == patt.size()) {
      return true;
    }
  }

  return false;
}

int main(int argc, char *argv[]) {
  string s, patt;
  std::cin >> patt >> s;
  if (kmp(s, patt)) {
    std::cout << "FOUND" << "\n";
  } else {
    std::cout << "NOT FOUND" << "\n";
  }

  return 0;
}
